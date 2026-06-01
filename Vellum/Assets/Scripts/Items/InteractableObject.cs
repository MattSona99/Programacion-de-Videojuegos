using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;

/// <summary>
/// Generic interactable: a trigger collider that shows an interaction prompt while the Player
/// is in range and invokes a UnityEvent when F is pressed. Behaviors are wired per-object in
/// the Inspector (no hard-coded coupling), per the project's interaction pattern.
/// </summary>
public class InteractableObject : MonoBehaviour
{
    [Header("UI settings (prompt bubble)")]
    public Sprite promptIcon;

    [Header("What happens when you press F?")]
    public UnityEvent onInteract; // Build the list in the Inspector

    private bool _isPlayerInRange = false;

    void Update()
    {
        if (_isPlayerInRange && Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
        {
            // Hide the prompt bubble while interacting
            if (InteractionUIManager.Instance != null)
                InteractionUIManager.Instance.HidePrompt();

            // Run the custom actions wired in the Inspector
            onInteract.Invoke();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _isPlayerInRange = true;
            if (InteractionUIManager.Instance != null)
                InteractionUIManager.Instance.ShowPrompt(other.transform, promptIcon);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _isPlayerInRange = false;
            if (InteractionUIManager.Instance != null)
                InteractionUIManager.Instance.HidePrompt();
        }
    }
}