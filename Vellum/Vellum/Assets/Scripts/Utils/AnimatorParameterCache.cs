using System.Collections.Generic;
using UnityEngine;

// Cache dei parametri esistenti su un Animator, per evitare i warning
// "Parameter '...' does not exist" quando un controller scaricato non ha i
// parametri attesi. Refresh() solo quando l'Animator cambia (non in hot path).
public sealed class AnimatorParameterCache
{
    private readonly HashSet<int> _hashes = new HashSet<int>();

    public void Refresh(Animator animator)
    {
        _hashes.Clear();
        if (animator == null) return;
        foreach (AnimatorControllerParameter p in animator.parameters)
            _hashes.Add(p.nameHash);
    }

    public bool Has(int hash) => _hashes.Contains(hash);
}
