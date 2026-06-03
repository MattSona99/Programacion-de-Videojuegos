using UnityEngine;

/// <summary>
/// Feeds <see cref="ScoreManager"/> from Level 3 (Mirror Duel) events. Drop this on any object in the
/// Act_03 scene and assign the references; it subscribes in <c>OnEnable</c> and unsubscribes in
/// <c>OnDisable</c>. The Level 3 timer starts here (scene load) and stops on win/loss; on a loss the
/// boss's final health is read for partial credit.
/// </summary>
public class ScoreLevel3Binder : MonoBehaviour
{
    [SerializeField] private MirrorDuelDirector director;
    [Tooltip("The Player's Health (damage taken).")]
    [SerializeField] private Health playerHealth;
    [Tooltip("Jammo's Health (damage taken during the duel).")]
    [SerializeField] private Health jammoHealth;
    [Tooltip("The boss's Health (read on loss for partial credit).")]
    [SerializeField] private Health bossHealth;
    [Tooltip("The Player's melee component (Accuracy: swings + hits).")]
    [SerializeField] private PlayerMeleeAttack playerMelee;
    [Tooltip("The Player's shield filter (blocks/parries).")]
    [SerializeField] private FrontalShieldBlock shield;

    private void OnEnable()
    {
        if (director != null)
        {
            director.EnteredMoon += HandleEnteredMoon;
            director.Won += HandleWon;
            director.Lost += HandleLost;
            director.PieceDelivered += HandlePieceDelivered;
            director.PieceLost += HandlePieceLost;
        }
        if (playerHealth != null) playerHealth.Damaged += HandlePlayerDamaged;
        if (jammoHealth != null) jammoHealth.Damaged += HandleJammoDamaged;
        if (playerMelee != null) { playerMelee.Swung += HandleSwing; playerMelee.HitLanded += HandleHit; }
        if (shield != null) shield.Blocked += HandleBlock;

        ScoreManager.Instance?.BeginLevelTimer(3);
    }

    private void OnDisable()
    {
        if (director != null)
        {
            director.EnteredMoon -= HandleEnteredMoon;
            director.Won -= HandleWon;
            director.Lost -= HandleLost;
            director.PieceDelivered -= HandlePieceDelivered;
            director.PieceLost -= HandlePieceLost;
        }
        if (playerHealth != null) playerHealth.Damaged -= HandlePlayerDamaged;
        if (jammoHealth != null) jammoHealth.Damaged -= HandleJammoDamaged;
        if (playerMelee != null) { playerMelee.Swung -= HandleSwing; playerMelee.HitLanded -= HandleHit; }
        if (shield != null) shield.Blocked -= HandleBlock;
    }

    private void HandleEnteredMoon() => ScoreManager.Instance?.SetReachedMoon();
    private void HandlePieceDelivered() => ScoreManager.Instance?.AddPieceDelivered();
    private void HandlePieceLost() => ScoreManager.Instance?.AddPieceLost();
    private void HandleSwing() => ScoreManager.Instance?.AddSwing();
    private void HandleHit() => ScoreManager.Instance?.AddHit();
    private void HandleBlock() => ScoreManager.Instance?.AddBlock(3);
    private void HandlePlayerDamaged(DamageInfo info) => ScoreManager.Instance?.AddPlayerDamageTaken(3, info.amount);
    private void HandleJammoDamaged(DamageInfo info) => ScoreManager.Instance?.AddJammoDamageTaken(info.amount);

    private void HandleWon()
    {
        if (ScoreManager.Instance == null) return;
        ScoreManager.Instance.SetBossResult(true, 0f); // defeated → 0 health
        ScoreManager.Instance.EndLevelTimer(3);
    }

    private void HandleLost()
    {
        if (ScoreManager.Instance == null) return;
        float bossHp = bossHealth != null ? bossHealth.Normalized : 0f;
        ScoreManager.Instance.SetBossResult(false, bossHp); // partial credit from remaining boss HP
        ScoreManager.Instance.EndLevelTimer(3);
    }
}
