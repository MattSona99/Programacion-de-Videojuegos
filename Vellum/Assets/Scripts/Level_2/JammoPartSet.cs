using System.Collections.Generic;
using UnityEngine;

// Sta sul prefab Jammo scala-1 usato come "pezzo" trasportato. Tiene il rig
// intero (la mesh skinnata rende solo se lo scheletro Mixamo è presente):
// ShowOnly mostra una sola parte e congela la posa, così visivamente è una
// singola parte solida fluttuante invece di un Jammo intero.
public class JammoPartSet : MonoBehaviour
{
    [Header("Renderer delle parti (nome = chiave di correlazione con StatueRig)")]
    [SerializeField] private Renderer[] parts;

    [Tooltip("Animator del rig. Se vuoto, GetComponentInChildren in Awake. Viene disabilitato per congelare la bind-pose.")]
    [SerializeField] private Animator animator;

    private readonly Dictionary<string, Renderer> _byName = new Dictionary<string, Renderer>();

    void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();

        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i] == null) continue;
            _byName[parts[i].gameObject.name] = parts[i];
        }
    }

    // Mostra solo la parte richiesta, nasconde le altre, congela la posa.
    public void ShowOnly(string partName)
    {
        if (animator != null) animator.enabled = false;

        bool found = false;
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i] == null) continue;
            bool match = parts[i].gameObject.name == partName;
            parts[i].enabled = match;
            if (match) found = true;
        }

        if (!found)
            Debug.LogWarning($"[JammoPartSet] Parte '{partName}' non trovata sul prefab scala-1.", this);
    }
}
