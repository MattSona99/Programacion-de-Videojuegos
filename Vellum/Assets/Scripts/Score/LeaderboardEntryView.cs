using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// View for a single leaderboard row (the "Player Entry" prefab). Fills the main row
/// (rank / name / score / grade), builds the four multiline stat blocks for the inline details
/// panel, and toggles that panel via its button (accordion). Populated by <see cref="LeaderboardUI"/>.
/// </summary>
public class LeaderboardEntryView : MonoBehaviour
{
    [Header("Main row")]
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text gradeText;
    [SerializeField] private Button detailsButton;

    [Header("Details (accordion)")]
    [Tooltip("Panel shown/hidden by the button. Hidden on Populate.")]
    [SerializeField] private GameObject detailsPanel;
    [SerializeField] private TMP_Text globalStatsText;
    [SerializeField] private TMP_Text l1StatsText;
    [SerializeField] private TMP_Text l2StatsText;
    [SerializeField] private TMP_Text l3StatsText;

    /// <summary>Fills the row from <paramref name="entry"/> and wires the details toggle. <paramref name="rank"/> is 1-based.</summary>
    public void Populate(int rank, LeaderboardEntry entry)
    {
        if (entry == null) return;

        if (rankText != null) rankText.text = $"{rank}.";
        if (nameText != null) nameText.text = entry.playerName;
        if (scoreText != null) scoreText.text = entry.finalScore.ToString();
        if (gradeText != null) gradeText.text = entry.grade;

        RunStats s = entry.stats;
        if (s != null)
        {
            if (globalStatsText != null) globalStatsText.text = BuildGlobal(s);
            if (l1StatsText != null) l1StatsText.text = BuildL1(s);
            if (l2StatsText != null) l2StatsText.text = BuildL2(s);
            if (l3StatsText != null) l3StatsText.text = BuildL3(s);
        }

        if (detailsPanel != null) detailsPanel.SetActive(false);
        if (detailsButton != null)
        {
            detailsButton.onClick.RemoveListener(ToggleDetails);
            detailsButton.onClick.AddListener(ToggleDetails);
        }

        ShowSection(0); // default to the Global tab
    }

    /// <summary>Expands/collapses this row's details panel (inline accordion).</summary>
    public void ToggleDetails()
    {
        if (detailsPanel != null) detailsPanel.SetActive(!detailsPanel.activeSelf);
    }

    /// <summary>
    /// Tab switch: shows only one stat section (0=Global, 1=L1, 2=L2, 3=L3) and hides the others.
    /// Wire the four tab buttons' OnClick to this with the matching static int. With a single section
    /// visible the DetailsPanel can be a fixed height (no dynamic stacking).
    /// </summary>
    public void ShowSection(int index)
    {
        if (globalStatsText != null) globalStatsText.gameObject.SetActive(index == 0);
        if (l1StatsText != null) l1StatsText.gameObject.SetActive(index == 1);
        if (l2StatsText != null) l2StatsText.gameObject.SetActive(index == 2);
        if (l3StatsText != null) l3StatsText.gameObject.SetActive(index == 3);
    }

    // ---- Stat-block formatting -------------------------------------------

    private static string YesNo(bool v) => v ? "Yes" : "No";

    private static string BuildGlobal(RunStats s)
    {
        GlobalStats g = s.global;
        var sb = new StringBuilder();
        sb.AppendLine("GLOBAL");
        sb.AppendLine($"Time: {g.totalTime:0.0}s");
        sb.AppendLine($"Damage dealt: {g.totalDamageDealt:0}");
        sb.AppendLine($"Damage taken: {g.totalDamageTaken:0}");
        sb.AppendLine($"Enemies killed: {g.totalEnemiesKilled}");
        sb.AppendLine($"Accuracy: {g.accuracyGlobal:P0}");
        sb.AppendLine($"Completion: {g.completionPercent:P0}");
        sb.Append($"Perfect health: {YesNo(g.perfectHealth)}");
        return sb.ToString();
    }

    private static string BuildL1(RunStats s)
    {
        LevelOneStats a = s.l1;
        var sb = new StringBuilder();
        sb.AppendLine($"LEVEL 1 — {s.level1Score}");
        sb.AppendLine($"Time: {a.time:0.0}s");
        sb.AppendLine($"Fallen: {a.fallen}");
        sb.AppendLine($"Hints used: {a.helpNeeded}");
        sb.AppendLine($"No-hint clear: {YesNo(a.noHintClear)}");
        sb.Append($"Perfect: {YesNo(a.perfect)}");
        return sb.ToString();
    }

    private static string BuildL2(RunStats s)
    {
        LevelTwoStats b = s.l2;
        var sb = new StringBuilder();
        sb.AppendLine($"LEVEL 2 — {s.level2Score}");
        sb.AppendLine($"Time: {b.time:0.0}s");
        sb.AppendLine($"Enemies killed: {b.enemiesKilled}");
        sb.AppendLine($"Waves cleared: {b.wavesCleared}");
        sb.AppendLine($"Statue parts: {b.statuePartsPlaced}");
        sb.AppendLine($"Statue completed: {YesNo(b.statueCompleted)}");
        sb.AppendLine($"Best streak: {b.bestKillStreak}");
        sb.AppendLine($"Blocks: {b.blocks}");
        sb.AppendLine($"Drops spawned: {b.healthDropsSpawned}");
        sb.AppendLine($"Drops collected: {b.healthDropsCollected}");
        sb.AppendLine($"Damage taken: {b.damageTaken:0}");
        sb.Append($"Perfect: {YesNo(b.perfect)}");
        return sb.ToString();
    }

    private static string BuildL3(RunStats s)
    {
        LevelThreeStats c = s.l3;
        var sb = new StringBuilder();
        sb.AppendLine($"LEVEL 3 — {s.level3Score}");
        sb.AppendLine($"Time: {c.time:0.0}s");
        sb.AppendLine($"Boss defeated: {YesNo(c.bossDefeated)}");
        sb.AppendLine($"Boss HP left: {c.bossHealthFinal:P0}");
        sb.AppendLine($"Reached Moon: {YesNo(c.reachedMoonPhase)}");
        sb.AppendLine($"Pieces delivered: {c.piecesDelivered}");
        sb.AppendLine($"Pieces lost: {c.piecesLost}");
        sb.AppendLine($"Blocks: {c.blocks}");
        sb.AppendLine($"Damage taken: {c.damageTaken:0}");
        sb.AppendLine($"Jammo damage: {c.jammoDamageTaken:0}");
        sb.Append($"Perfect: {YesNo(c.perfect)}");
        return sb.ToString();
    }
}
