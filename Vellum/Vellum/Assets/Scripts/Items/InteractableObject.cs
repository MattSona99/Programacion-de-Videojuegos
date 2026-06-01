using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events; // <-- IL SEGRETO È QUI

public class InteractableObject : MonoBehaviour
{
    [Header("Impostazioni UI (Nuvoletta)")]
    public Sprite promptIcon;
    
    [Header("Cosa succede quando premi F?")]
    public UnityEvent onInteract; // <-- Crea la lista nell'Inspector!

    private bool _isPlayerInRange = false;

    void Update()
    {
        if (_isPlayerInRange && Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
        {
            // Nasconde la nuvoletta mentre interagisci
            if (InteractionUIManager.Instance != null)
                InteractionUIManager.Instance.HidePrompt();
                
            // ESEGUE LE AZIONI PERSONALIZZATE CHE SCEGLI NELL'INSPECTOR!
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