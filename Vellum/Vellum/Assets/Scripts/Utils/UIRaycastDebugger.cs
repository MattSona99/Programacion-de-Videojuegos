using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.InputSystem; // Fondamentale per il nuovo sistema!

public class UIRaycastDebugger : MonoBehaviour
{
    void Update()
    {
        // Controlliamo se il mouse esiste e se il tasto sinistro è stato premuto in questo frame
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            PointerEventData eventData = new PointerEventData(EventSystem.current);
            // Prendiamo la posizione del mouse dal nuovo Input System
            eventData.position = Mouse.current.position.ReadValue();

            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            Debug.Log("--- INIZIO RAYCAST DEBUG ---");
            if (results.Count > 0)
            {
                foreach (RaycastResult result in results)
                {
                    if (result.gameObject.GetComponent<Graphic>() != null)
                    {
                        Debug.Log("STAI TOCCANDO: <color=yellow>" + result.gameObject.name + "</color> (Tag: " + result.gameObject.tag + ")");
                        
                        Graphic g = result.gameObject.GetComponent<Graphic>();
                        if (g.color.a == 0)
                        {
                            Debug.LogWarning("ATTENZIONE: L'oggetto <color=red>" + result.gameObject.name + "</color> è COMPLETAMENTE TRASPARENTE e sta bloccando il clic!");
                        }
                    }
                }
            }
            else
            {
                Debug.Log("Non stai toccando nessun elemento UI.");
            }
            Debug.Log("--- FINE RAYCAST DEBUG ---");
        }
    }
}