using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sits on the Player root. <see cref="PlayerCombat"/> calls BeginSwing() on left click.
/// Hit detection is input-driven (NOT via Animation Event): independent of the clip/Animator
/// and identical for the male and female meshes.
/// </summary>
public class PlayerMeleeAttack : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] private float attackDamage = 15f;

    [Header("Swing timing (seconds)")]
    [Tooltip("Delay from the click before the hitbox becomes active (attack wind-up).")]
    [SerializeField] private float windupTime = 0.15f;
    [Tooltip("Duration the hitbox stays active and can hit.")]
    [SerializeField] private float activeTime = 0.15f;

    [Header("Hitbox (relative to the Player)")]
    [SerializeField] private Vector3 hitboxHalfExtents = new Vector3(0.6f, 0.7f, 0.8f);
    [SerializeField] private float forwardOffset = 1.0f;
    [SerializeField] private float verticalOffset = 1.0f;

    [Tooltip("Set to the enemy/statue layers. Leave as Everything if you don't use dedicated layers.")]
    [SerializeField] private LayerMask hittableLayers = ~0;

    private readonly Collider[] _hits = new Collider[16];
    private readonly List<IDamageable> _damagedThisSwing = new List<IDamageable>(8);
    private Coroutine _swing;

    /// <summary>
    /// True during a swing's wind-up + active window. Read by the Boss (BossDuelAI) for
    /// reactive defense: it sees the swing start and can raise its guard in advance.
    /// </summary>
    public bool IsSwinging { get; private set; }

    /// <summary>Raised when a swing starts. Used by the scoring system as the Accuracy denominator.</summary>
    public event Action Swung;

    /// <summary>Raised once per distinct target actually hit in a swing. Accuracy numerator.</summary>
    public event Action HitLanded;

    /// <summary>
    /// Called by PlayerCombat on left click. <paramref name="damageOverride"/> &gt; 0 is used
    /// instead of the default (useful for per-hit combo damage).
    /// </summary>
    public void BeginSwing(float damageOverride = 0f)
    {
        if (_swing != null) StopCoroutine(_swing);
        Swung?.Invoke();
        _swing = StartCoroutine(SwingRoutine(damageOverride > 0f ? damageOverride : attackDamage));
    }

    /// <summary>
    /// Cancels the in-progress swing (e.g. when the Player raises the shield): prevents the
    /// "chambered" hit — whose damage is on an animation-independent timer — from still
    /// landing after defending.
    /// </summary>
    public void CancelSwing()
    {
        if (_swing != null) { StopCoroutine(_swing); _swing = null; }
        IsSwinging = false;
    }

    /// <summary>Wind-up, then keeps the hitbox active for <see cref="activeTime"/>, applying hits each frame.</summary>
    private IEnumerator SwingRoutine(float dmg)
    {
        _damagedThisSwing.Clear();
        IsSwinging = true;

        if (windupTime > 0f) yield return new WaitForSeconds(windupTime);

        float elapsed = 0f;
        while (elapsed < activeTime)
        {
            ApplyHit(dmg);
            elapsed += Time.deltaTime;
            yield return null;
        }

        IsSwinging = false;
        _swing = null;
    }

    /// <summary>Overlaps the forward hitbox and damages each unique <see cref="IDamageable"/> once per swing.</summary>
    private void ApplyHit(float dmg)
    {
        Vector3 center = transform.position
                         + Vector3.up * verticalOffset
                         + transform.forward * forwardOffset;

        int count = Physics.OverlapBoxNonAlloc(
            center, hitboxHalfExtents, _hits, transform.rotation,
            hittableLayers, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider col = _hits[i];
            if (col == null) continue;
            if (col.transform == transform || col.transform.IsChildOf(transform)) continue; // never self

            IDamageable target = col.GetComponentInParent<IDamageable>();
            if (target == null) continue;
            if (_damagedThisSwing.Contains(target)) continue; // one hit per target per swing

            _damagedThisSwing.Add(target);
            target.TakeDamage(new DamageInfo(dmg, transform.position, gameObject));
            HitLanded?.Invoke();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position
                         + Vector3.up * verticalOffset
                         + transform.forward * forwardOffset;
        Gizmos.color = Color.red;
        Gizmos.matrix = Matrix4x4.TRS(center, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, hitboxHalfExtents * 2f);
    }
}
