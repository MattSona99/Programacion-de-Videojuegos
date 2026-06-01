using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.InputSystem; // Required for the new Input System!

/// <summary>
/// Debug helper: on left mouse click, raycasts the UI under the cursor and logs every
/// hit Graphic, flagging fully transparent elements that may be silently blocking clicks.
/// </summary>
public class UIRaycastDebugger : MonoBehaviour
{
    void Update()
    {
        // Check whether the mouse exists and the left button was pressed this frame
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            PointerEventData eventData = new PointerEventData(EventSystem.current);
            // Read the mouse position from the new Input System
            eventData.position = Mouse.current.position.ReadValue();

            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            Debug.Log("--- RAYCAST DEBUG START ---");
            if (results.Count > 0)
            {
                foreach (RaycastResult result in results)
                {
                    if (result.gameObject.GetComponent<Graphic>() != null)
                    {
                        Debug.Log("YOU ARE TOUCHING: <color=yellow>" + result.gameObject.name + "</color> (Tag: " + result.gameObject.tag + ")");

                        Graphic g = result.gameObject.GetComponent<Graphic>();
                        if (g.color.a == 0)
                        {
                            Debug.LogWarning("WARNING: object <color=red>" + result.gameObject.name + "</color> is FULLY TRANSPARENT and is blocking the click!");
                        }
                    }
                }
            }
            else
            {
                Debug.Log("You are not touching any UI element.");
            }
            Debug.Log("--- RAYCAST DEBUG END ---");
        }
    }
}