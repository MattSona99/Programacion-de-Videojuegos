using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Coordinates who the arena enemies target. Rules:
/// - default target = Jammo (enemies aim to sabotage the statue);
/// - an enemy switches to the Player only if "triggered by the Player" (it was hit by them)
///   and there are free Player slots (maxPlayerChasers). So as soon as one engages the
///   Player, the rest of the wave stay on / go to Jammo.
/// Lightweight singleton: pooled enemies register via Instance with no per-instance wiring.
/// The Player/Jammo references are assigned in the Inspector.
/// </summary>
public class EnemyTargetCoordinator : MonoBehaviour
{
    public static EnemyTargetCoordinator Instance { get; private set; }

    [Header("Targets")]
    [SerializeField] private Transform player;
    [SerializeField] private Health playerHealth;
    [SerializeField] private Transform jammo;
    [SerializeField] private Health jammoHealth;

    [Header("Engagement rules")]
    [Tooltip("How many enemies can chase the Player at once. The rest go to Jammo.")]
    [SerializeField] private int maxPlayerChasers = 1;

    // ORDERED lists: the index in the list is the "slot" used for the fan-shaped
    // encirclement (see EnemyAI.ComputeApproachPoint).
    private readonly List<EnemyAI> _playerChasers = new List<EnemyAI>();
    private readonly List<EnemyAI> _jammoChasers = new List<EnemyAI>();

    public Transform Player => player;
    public Health PlayerHealth => playerHealth;
    public Transform Jammo => jammo;
    public Health JammoHealth => jammoHealth;

    public int PlayerChaserCount => _playerChasers.Count;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[EnemyTargetCoordinator] Duplicate instance: keeping only one.", this);
            Destroy(this);
            return;
        }
        Instance = this;

        if (player != null && playerHealth == null) playerHealth = player.GetComponentInParent<Health>();
        if (jammo != null && jammoHealth == null) jammoHealth = jammo.GetComponentInParent<Health>();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public bool JammoAlive => jammo != null && (jammoHealth == null || !jammoHealth.IsDead);
    public bool PlayerAlive => player != null && (playerHealth == null || !playerHealth.IsDead);

    /// <summary>
    /// Tries to reserve a Player slot for this enemy. True if granted (or already held).
    /// False → the enemy must target Jammo.
    /// </summary>
    public bool TryClaimPlayer(EnemyAI enemy)
    {
        if (enemy == null || !PlayerAlive) return false;
        if (_playerChasers.Contains(enemy)) return true;
        if (_playerChasers.Count >= maxPlayerChasers) return false;

        _jammoChasers.Remove(enemy);
        _playerChasers.Add(enemy);
        return true;
    }

    /// <summary>Releases this enemy's Player slot.</summary>
    public void ReleasePlayer(EnemyAI enemy)
    {
        if (enemy != null) _playerChasers.Remove(enemy);
    }

    /// <summary>The enemy targets Jammo: enters the jammo list (and leaves the player list).</summary>
    public void RegisterJammo(EnemyAI enemy)
    {
        if (enemy == null) return;
        _playerChasers.Remove(enemy);
        if (!_jammoChasers.Contains(enemy)) _jammoChasers.Add(enemy);
    }

    /// <summary>Idle / death / disable: removes the enemy from both lists.</summary>
    public void Unregister(EnemyAI enemy)
    {
        if (enemy == null) return;
        _playerChasers.Remove(enemy);
        _jammoChasers.Remove(enemy);
    }

    /// <summary>True if the enemy currently holds a Player slot.</summary>
    public bool IsChasingPlayer(EnemyAI enemy) => enemy != null && _playerChasers.Contains(enemy);

    /// <summary>Slot index in the target's list — used to fan the enemies out around the target.</summary>
    public int SlotIndex(EnemyAI enemy, bool isPlayer)
        => (isPlayer ? _playerChasers : _jammoChasers).IndexOf(enemy);

    /// <summary>Number of attackers currently targeting the Player (or Jammo).</summary>
    public int AttackerCount(bool isPlayer)
        => (isPlayer ? _playerChasers : _jammoChasers).Count;
}
