using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sits on the scale-1 Jammo prefab used as the carried "piece". It holds the full rig (the
/// skinned mesh only renders if the Mixamo skeleton is present): ShowOnly displays a single
/// part and freezes the pose, so visually it's a single solid floating part instead of a whole Jammo.
/// </summary>
public class JammoPartSet : MonoBehaviour
{
    [Header("Part renderers (name = correlation key with StatueRig)")]
    [SerializeField] private Renderer[] parts;

    [Tooltip("Rig Animator. If empty, GetComponentInChildren in Awake. Disabled to freeze the bind pose.")]
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

    /// <summary>Shows only the requested part, hides the others, and freezes the pose.</summary>
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
            Debug.LogWarning($"[JammoPartSet] Part '{partName}' not found on the scale-1 prefab.", this);
    }
}
