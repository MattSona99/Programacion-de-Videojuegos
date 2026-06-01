using System;
using UnityEngine;

/// <summary>
/// Health pickup. Spawned by the WaveManager when an enemy dies, with a configurable chance
/// (see WaveManager.healthDropChance). The visual prefab (mesh/sprite + trigger Collider) is
/// authored in the Editor; the script is look-agnostic. Pool-friendly: SetActive(false) via an
/// owner callback (no Destroy, CLAUDE.md §4.3).
/// </summary>
[RequireComponent(typeof(Collider))]
public class HealthPickup : MonoBehaviour
{
    // Who gets healed on collection. In both cases the pickup is collected by the Player
    // walking over it; only the recipient of the heal changes.
    private enum HealTarget { Player, Jammo }

    [Header("Pickup")]
    [Tooltip("Player → heals whoever collects it (the Player). Jammo → heals Jammo (repair kit collected by the Player).")]
    [SerializeField] private HealTarget healTarget = HealTarget.Player;
    [SerializeField] private float healAmount = 50f;
    [Tooltip("Player tag. The pickup only triggers on contact with this tag.")]
    [SerializeField] private string playerTag = "Player";

    private Action _onCollected;
    private bool _consumed;

    /// <summary>
    /// Configured by the WaveManager when spawned from the pool: the callback returns the
    /// pickup to the pool when collected. Also resets the _consumed flag for reuse.
    /// </summary>
    public void Configure(Action onCollected)
    {
        _onCollected = onCollected;
        _consumed = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_consumed) return;
        if (!other.CompareTag(playerTag)) return;

        Health target = ResolveHealTarget(other);
        if (target == null) return;

        target.Heal(healAmount);
        _consumed = true;
        _onCollected?.Invoke();
    }

    private Health ResolveHealTarget(Collider collector)
    {
        if (healTarget == HealTarget.Player)
            return collector.GetComponentInParent<Health>();

        // Jammo: the heal goes to Jammo (registered in the enemy coordinator),
        // not to the Player who physically collects the pickup.
        var coord = EnemyTargetCoordinator.Instance;
        return coord != null ? coord.JammoHealth : null;
    }
}
