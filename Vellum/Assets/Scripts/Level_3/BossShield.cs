using UnityEngine;

/// <summary>
/// Final-level boss shield. An IDamageFilter on Health with two roles:
///   1) FRONTAL BLOCK when the boss is defending (SetDefending), like FrontalShieldBlock for the
///      Player: the Player's frontal hits are cancelled (the active "defense" driven by BossDuelAI).
///   2) HIT COUNT during collection (SetShedding): every 'hitsPerPiece' of the Player's hits that
///      LAND (not blocked) tells the director to unlock a piece.
///
/// No more total invulnerability during collection: the boss is now hittable
/// (Health.SetMaxDamagePerHit(1) from the director → 1 HP/hit). A 'blockAll' remains, used only as
/// safety during the sky flip.
///
/// NB: keep Health.invulnerabilityDuration = 0 on the boss so every Player swing (one hit per
/// target per swing) is counted.
/// </summary>
[RequireComponent(typeof(Health))]
public class BossShield : MonoBehaviour, IDamageFilter
{
    [SerializeField] private MirrorDuelDirector director;

    [Tooltip("Player hits that land to shed a piece (≈ combo length).")]
    [SerializeField] private int hitsPerPiece = 3;

    [Tooltip("Total width of the frontal cone within which the guard blocks, in degrees.")]
    [SerializeField] private float blockAngle = 120f;

    private bool _blockAll;     // safety during the sky flip
    private bool _defending;    // guard raised by BossDuelAI
    private bool _shedding;     // collection phase: counts hits → pieces
    private int _hitCount;

    /// <summary>True while the boss guard is raised.</summary>
    public bool IsDefending => _defending;

    /// <summary>Temporary total block (e.g. during the sky flip).</summary>
    public void SetInvulnerable(bool value) => _blockAll = value;

    /// <summary>Active guard: blocks the Player's frontal hits. Driven by BossDuelAI (defense phases + reactive defense).</summary>
    public void SetDefending(bool value) => _defending = value;

    /// <summary>Enables hit→piece counting (only during collection). Resets the counter on (de)activation so phases don't inherit partial hits.</summary>
    public void SetShedding(bool value)
    {
        _shedding = value;
        _hitCount = 0;
    }

    /// <summary>Filters incoming damage: blocks frontal hits while defending, and counts landed hits toward shedding a piece.</summary>
    public bool ShouldBlock(DamageInfo info)
    {
        if (_blockAll) return true;

        bool fromPlayer = info.source != null && info.source.CompareTag("Player");

        // Active defense: blocks the Player's frontal hits (they don't count, don't deal
        // damage). Pushes the Player to strike when the guard is down.
        if (_defending && fromPlayer && IsFrontal(info)) return true;

        // Hit landed during collection: counts toward unlocking a piece.
        if (fromPlayer && _shedding)
        {
            _hitCount++;
            if (_hitCount >= hitsPerPiece)
            {
                _hitCount = 0;
                if (director != null) director.NotifyPlayerBrokePiece();
            }
        }

        return false; // Health applies the damage (capped to 1 during collection, with the phase floor)
    }

    private bool IsFrontal(DamageInfo info)
    {
        Vector3 toSource = info.sourcePosition - transform.position;
        toSource.y = 0f;
        if (toSource.sqrMagnitude < 0.0001f) return true; // above the boss: treat as frontal
        return Vector3.Angle(transform.forward, toSource.normalized) <= blockAngle * 0.5f;
    }
}
