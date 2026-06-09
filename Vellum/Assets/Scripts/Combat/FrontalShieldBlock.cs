using System;
using UnityEngine;

/// <summary>
/// Goes on the Player, next to <see cref="Health"/>. As an <see cref="IDamageFilter"/>
/// it cancels incoming damage when the shield is raised (<see cref="PlayerCombat.IsDefending"/>)
/// and the attacker is within the frontal cone.
/// </summary>
[RequireComponent(typeof(Health))]
public class FrontalShieldBlock : MonoBehaviour, IDamageFilter
{
    [Tooltip("Total width of the frontal blocking cone, in degrees.")]
    [SerializeField] private float blockAngle = 120f;

    private PlayerCombat _combat;

    /// <summary>Raised each time a hit is actually blocked. Used by the scoring system (Blocks/parries).</summary>
    public event Action Blocked;

    void Awake()
    {
        _combat = GetComponent<PlayerCombat>();
        if (_combat == null) _combat = GetComponentInParent<PlayerCombat>();
    }

    /// <summary>Blocks the hit only while defending and the source lies within the frontal cone.</summary>
    public bool ShouldBlock(DamageInfo info)
    {
        if (_combat == null || !_combat.IsDefending) return false;

        Vector3 toSource = info.sourcePosition - transform.position;
        toSource.y = 0f;

        // Directly above the player counts as frontal; otherwise require the source within the cone.
        bool blocked = toSource.sqrMagnitude < 0.0001f
                       || Vector3.Angle(transform.forward, toSource.normalized) <= blockAngle * 0.5f;

        if (blocked) Blocked?.Invoke();
        return blocked;
    }
}
