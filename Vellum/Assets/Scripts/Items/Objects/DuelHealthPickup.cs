using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// NEUTRAL health pickup for the final duel: heals whichever of the two contenders (Player or
/// Boss) walks over it. Unlike HealthPickup (tied to the Player tag / arena coordinator), the
/// two valid Health components here are injected by the spawner, so it never heals Jammo nor
/// triggers on the scene pieces. Pool-friendly (no Destroy, CLAUDE.md §4.3); the look is authored
/// in the Editor.
/// </summary>
[RequireComponent(typeof(Collider))]
public class DuelHealthPickup : MonoBehaviour
{
    private Action _onRemoved;
    private Health _a;
    private Health _b;
    private float _healAmount;
    private bool _consumed;
    private Coroutine _lifetimeRoutine;

    /// <summary>
    /// Instant (Time.time) the pickup was activated: the boss only "notices" it after a
    /// reaction delay (see BossDuelAI.pickupReactionDelay).
    /// </summary>
    public float SpawnTime { get; private set; }

    /// <summary>
    /// Configured by the spawner on spawn from the pool. onRemoved returns the pickup to the
    /// pool and deregisters it (both on collection and on lifetime expiry).
    /// </summary>
    public void Configure(Action onRemoved, Health a, Health b, float healAmount, float lifetime)
    {
        _onRemoved = onRemoved;
        _a = a;
        _b = b;
        _healAmount = healAmount;
        _consumed = false;
        SpawnTime = Time.time;

        if (_lifetimeRoutine != null) StopCoroutine(_lifetimeRoutine);
        if (lifetime > 0f) _lifetimeRoutine = StartCoroutine(LifetimeRoutine(lifetime));
    }

    private IEnumerator LifetimeRoutine(float lifetime)
    {
        yield return new WaitForSeconds(lifetime);
        _lifetimeRoutine = null;
        Remove();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_consumed) return;

        Health entered = other.GetComponentInParent<Health>();
        if (entered == null || entered.IsDead) return;
        if (entered != _a && entered != _b) return; // only Player or Boss

        entered.Heal(_healAmount);
        Remove();
    }

    private void Remove()
    {
        if (_consumed) return;
        _consumed = true;
        if (_lifetimeRoutine != null) { StopCoroutine(_lifetimeRoutine); _lifetimeRoutine = null; }
        _onRemoved?.Invoke();
    }
}
