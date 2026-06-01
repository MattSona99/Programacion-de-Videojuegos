using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Caches the parameters that exist on an Animator, to avoid the
/// "Parameter '...' does not exist" warnings when a downloaded controller lacks the
/// expected parameters. Refresh() only when the Animator changes (not in a hot path).
/// </summary>
public sealed class AnimatorParameterCache
{
    private readonly HashSet<int> _hashes = new HashSet<int>();

    /// <summary>Rebuilds the cache from the given Animator's current parameter set.</summary>
    public void Refresh(Animator animator)
    {
        _hashes.Clear();
        if (animator == null) return;
        foreach (AnimatorControllerParameter p in animator.parameters)
            _hashes.Add(p.nameHash);
    }

    /// <summary>True if the Animator has a parameter with the given name hash.</summary>
    public bool Has(int hash) => _hashes.Contains(hash);
}
