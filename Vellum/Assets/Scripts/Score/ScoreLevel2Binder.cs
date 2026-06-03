using UnityEngine;

/// <summary>
/// Feeds <see cref="ScoreManager"/> from Level 2 (Arena + Statue) events. Drop this on any object in
/// the Act_02 scene and assign the references; it subscribes in <c>OnEnable</c> and unsubscribes in
/// <c>OnDisable</c>. The Level 2 timer starts here (scene load) and stops when the statue completes.
/// </summary>
public class ScoreLevel2Binder : MonoBehaviour
{
    [SerializeField] private WaveManager waveManager;
    [SerializeField] private StatueRig statueRig;
    [Tooltip("The arena Player's Health (damage taken).")]
    [SerializeField] private Health playerHealth;
    [Tooltip("The Player's melee component (Accuracy: swings + hits).")]
    [SerializeField] private PlayerMeleeAttack playerMelee;
    [Tooltip("The Player's shield filter (blocks/parries).")]
    [SerializeField] private FrontalShieldBlock shield;

    private void OnEnable()
    {
        if (waveManager != null)
        {
            waveManager.EnemyKilled += HandleEnemyKilled;
            waveManager.WaveCompleted += HandleWaveCleared;
            waveManager.HealthDropSpawned += HandleDropSpawned;
        }
        if (statueRig != null)
        {
            statueRig.PartRevealed += HandlePartPlaced;
            statueRig.StatueCompleted += HandleStatueComplete;
        }
        if (playerHealth != null) playerHealth.Damaged += HandlePlayerDamaged;
        if (playerMelee != null) { playerMelee.Swung += HandleSwing; playerMelee.HitLanded += HandleHit; }
        if (shield != null) shield.Blocked += HandleBlock;
        HealthPickup.Collected += HandlePickupCollected;

        ScoreManager.Instance?.BeginLevelTimer(2);
    }

    private void OnDisable()
    {
        if (waveManager != null)
        {
            waveManager.EnemyKilled -= HandleEnemyKilled;
            waveManager.WaveCompleted -= HandleWaveCleared;
            waveManager.HealthDropSpawned -= HandleDropSpawned;
        }
        if (statueRig != null)
        {
            statueRig.PartRevealed -= HandlePartPlaced;
            statueRig.StatueCompleted -= HandleStatueComplete;
        }
        if (playerHealth != null) playerHealth.Damaged -= HandlePlayerDamaged;
        if (playerMelee != null) { playerMelee.Swung -= HandleSwing; playerMelee.HitLanded -= HandleHit; }
        if (shield != null) shield.Blocked -= HandleBlock;
        HealthPickup.Collected -= HandlePickupCollected;
    }

    private void HandleEnemyKilled() => ScoreManager.Instance?.AddEnemyKill();
    private void HandleWaveCleared() => ScoreManager.Instance?.AddWaveCleared();
    private void HandleDropSpawned() => ScoreManager.Instance?.AddHealthDropSpawned();
    private void HandlePartPlaced() => ScoreManager.Instance?.AddStatuePartPlaced();
    private void HandleSwing() => ScoreManager.Instance?.AddSwing();
    private void HandleHit() => ScoreManager.Instance?.AddHit();
    private void HandleBlock() => ScoreManager.Instance?.AddBlock(2);
    private void HandlePlayerDamaged(DamageInfo info) => ScoreManager.Instance?.AddPlayerDamageTaken(2, info.amount);

    private void HandleStatueComplete()
    {
        if (ScoreManager.Instance == null) return;
        ScoreManager.Instance.SetStatueCompleted();
        ScoreManager.Instance.EndLevelTimer(2);
    }

    // Only Player heals count as the "regenerated life" malus; Jammo repair kits don't.
    private void HandlePickupCollected(bool healedPlayer)
    {
        if (healedPlayer) ScoreManager.Instance?.AddHealthDropCollected();
    }
}
