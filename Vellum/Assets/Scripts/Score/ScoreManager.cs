using System;
using UnityEngine;

/// <summary>
/// Run-wide score accumulator and leaderboard writer. Implements the additive scoring model from
/// <c>docs/punteggio.md</c>: it gathers per-level and global stats during a playthrough, computes the
/// per-level scores + bonus + final score + grade, and writes a full <see cref="LeaderboardEntry"/>.
///
/// Auto-bootstrapped (<see cref="RuntimeInitializeOnLoadMethod"/>) into a <c>DontDestroyOnLoad</c>
/// object, so it survives Act_01→02→03 with NO scene placement or manual wiring. The public mutation
/// API is ready for the per-level binders (added later); nothing calls it yet.
/// </summary>
public class ScoreManager : MonoBehaviour
{
    /// <summary>Global access point (auto-created before the first scene loads).</summary>
    public static ScoreManager Instance { get; private set; }

    [Header("Par times (seconds) — over par applies a time penalty (tune in playtest)")]
    [SerializeField] private float parTime1 = 60f;
    [SerializeField] private float parTime2 = 120f;
    [SerializeField] private float parTime3 = 180f;

    // Per-level time-penalty weights (grow with the level: the final duel's pace matters most).
    private const float TIME_WEIGHT_1 = 2f;
    private const float TIME_WEIGHT_2 = 2f;
    private const float TIME_WEIGHT_3 = 4f;

    // Grade thresholds on the final score (tune in playtest).
    private const int GRADE_S = 3200;
    private const int GRADE_A = 2500;
    private const int GRADE_B = 1800;
    private const int GRADE_C = 1100;

    /// <summary>Stats of the run in progress. Replaced (not mutated) by <see cref="ResetRun"/>.</summary>
    public RunStats Current { get; private set; } = new RunStats();

    private int _activeTimerLevel; // 0 = none; 1/2/3 = the level whose timer is running
    private int _currentStreak;    // live kill streak (reset when the Player takes damage)

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        GameObject go = new GameObject("ScoreManager");
        go.AddComponent<ScoreManager>(); // Awake sets Instance + DontDestroyOnLoad
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Time accrues with SCALED deltaTime, so the pause menu (timeScale=0) freezes it for free.
    private void Update()
    {
        if (_activeTimerLevel == 0) return;
        float dt = Time.deltaTime;
        switch (_activeTimerLevel)
        {
            case 1: Current.l1.time += dt; break;
            case 2: Current.l2.time += dt; break;
            case 3: Current.l3.time += dt; break;
        }
    }

    // ---- Timers -----------------------------------------------------------

    /// <summary>Starts accruing time for <paramref name="level"/> (1/2/3); stops any other level timer.</summary>
    public void BeginLevelTimer(int level) => _activeTimerLevel = level;

    /// <summary>Stops the timer if <paramref name="level"/> is the one currently running.</summary>
    public void EndLevelTimer(int level) { if (_activeTimerLevel == level) _activeTimerLevel = 0; }

    /// <summary>Snapshots the Level 1 time at the mid-level checkpoint (display-only split).</summary>
    public void MarkLevel1Checkpoint()
    {
        Current.l1.timeToCheckpoint = Current.l1.time;
        Current.l1.timeCheckpointToEnd = 0f; // filled at level end (time − toCheckpoint)
    }

    // ---- Combat (cross-level) --------------------------------------------

    /// <summary>Counts a melee swing started (Accuracy denominator).</summary>
    public void AddSwing() => Current.global.totalSwings++;

    /// <summary>Counts a melee swing that landed on a target (Accuracy numerator).</summary>
    public void AddHit() => Current.global.totalHits++;

    /// <summary>Adds damage the Player dealt to enemies/boss (global stat).</summary>
    public void AddDamageDealt(float amount) => Current.global.totalDamageDealt += Mathf.Max(0f, amount);

    /// <summary>Adds damage the Player took in <paramref name="level"/>; also breaks the kill streak.</summary>
    public void AddPlayerDamageTaken(int level, float amount)
    {
        amount = Mathf.Max(0f, amount);
        Current.global.totalDamageTaken += amount;
        if (level == 2) Current.l2.damageTaken += amount;
        else if (level == 3) Current.l3.damageTaken += amount;
        BreakStreak();
    }

    /// <summary>Adds damage Jammo took during the duel (Level 3 penalty input).</summary>
    public void AddJammoDamageTaken(float amount) => Current.l3.jammoDamageTaken += Mathf.Max(0f, amount);

    /// <summary>Counts a successful shield block/parry in <paramref name="level"/> (2 or 3).</summary>
    public void AddBlock(int level)
    {
        if (level == 2) Current.l2.blocks++;
        else if (level == 3) Current.l3.blocks++;
    }

    /// <summary>Registers an enemy kill in the arena (Level 2): counts it and extends the kill streak.</summary>
    public void AddEnemyKill()
    {
        Current.l2.enemiesKilled++;
        Current.global.totalEnemiesKilled++;
        _currentStreak++;
        if (_currentStreak > Current.l2.bestKillStreak) Current.l2.bestKillStreak = _currentStreak;
    }

    /// <summary>Resets the live kill streak (the Player took damage).</summary>
    public void BreakStreak() => _currentStreak = 0;

    // ---- Level 1 ----------------------------------------------------------

    /// <summary>Counts a path fail/reset (wrong step) in Level 1.</summary>
    public void AddFallen() => Current.l1.fallen++;

    /// <summary>Counts a memory-hint use in Level 1.</summary>
    public void AddHelpUsed() => Current.l1.helpNeeded++;

    // ---- Level 2 ----------------------------------------------------------

    /// <summary>Counts a cleared wave (loops included).</summary>
    public void AddWaveCleared() => Current.l2.wavesCleared++;

    /// <summary>Counts a statue piece placed by Jammo.</summary>
    public void AddStatuePartPlaced() => Current.l2.statuePartsPlaced++;

    /// <summary>Counts a health pickup dropped by a killed enemy.</summary>
    public void AddHealthDropSpawned() => Current.l2.healthDropsSpawned++;

    /// <summary>Counts a health pickup collected by the Player.</summary>
    public void AddHealthDropCollected() => Current.l2.healthDropsCollected++;

    /// <summary>Marks the statue completed (Level 2 finished).</summary>
    public void SetStatueCompleted() => Current.l2.statueCompleted = true;

    // ---- Level 3 ----------------------------------------------------------

    /// <summary>Counts a duel piece delivered to the Player's altar (summed over both phases).</summary>
    public void AddPieceDelivered() => Current.l3.piecesDelivered++;

    /// <summary>Counts a piece lost in the Moon phase (Jammo hit while carrying).</summary>
    public void AddPieceLost() => Current.l3.piecesLost++;

    /// <summary>Marks that the duel reached the Moon phase.</summary>
    public void SetReachedMoon() => Current.l3.reachedMoonPhase = true;

    /// <summary>
    /// Records the duel outcome: whether the boss was defeated and its final normalized health (for
    /// partial credit on loss). A defeat-by-Player also counts toward total enemies killed.
    /// </summary>
    public void SetBossResult(bool defeated, float bossHealthNormalized)
    {
        Current.l3.bossDefeated = defeated;
        Current.l3.bossHealthFinal = Mathf.Clamp01(bossHealthNormalized);
        if (defeated) Current.global.totalEnemiesKilled++;
    }

    // ---- Level completion -------------------------------------------------

    /// <summary>Marks a level as completed (Level 1; Level 2/3 are implied by statue/boss results).</summary>
    public void MarkLevelCompleted(int level)
    {
        if (level == 1) Current.l1.completed = true;
    }

    // ---- Scoring ----------------------------------------------------------

    /// <summary>
    /// Computes the derived flags, the per-level scores, the bonus, the final score and the grade
    /// into <see cref="Current"/>, following the additive formula in <c>docs/punteggio.md</c>.
    /// </summary>
    public void ComputeScores()
    {
        GlobalStats g = Current.global;
        LevelOneStats a = Current.l1;
        LevelTwoStats b = Current.l2;
        LevelThreeStats c = Current.l3;

        // Derived flags (computed from raw stats so they can't drift).
        a.noHintClear = a.helpNeeded == 0;
        a.perfect = a.completed && a.fallen == 0 && a.helpNeeded == 0;
        b.perfect = b.statueCompleted && Mathf.Approximately(b.damageTaken, 0f);
        c.perfect = c.bossDefeated && Mathf.Approximately(c.damageTaken, 0f) && c.piecesLost == 0;
        g.perfectHealth = Mathf.Approximately(b.damageTaken, 0f) && Mathf.Approximately(c.damageTaken, 0f);
        g.accuracyGlobal = g.AccuracyGlobal;
        g.totalTime = a.time + b.time + c.time;
        a.timeCheckpointToEnd = Mathf.Max(0f, a.time - a.timeToCheckpoint); // display-only split



        int levelsDone = (a.completed ? 1 : 0) + (b.statueCompleted ? 1 : 0) + (c.bossDefeated ? 1 : 0);
        g.completionPercent = levelsDone / 3f;

        float l1 = (a.completed ? 600f : 0f)
                 - a.fallen * 40f
                 - a.helpNeeded * 75f
                 - Mathf.Max(0f, a.time - parTime1) * TIME_WEIGHT_1
                 + (a.noHintClear ? 150f : 0f)
                 + (a.perfect ? 200f : 0f);

        float l2 = b.enemiesKilled * 10f
                 + b.wavesCleared * 30f
                 + b.statuePartsPlaced * 40f
                 + (b.statueCompleted ? 200f : 0f)
                 + b.bestKillStreak * 15f
                 + b.blocks * 10f
                 + b.healthDropsSpawned * 20f
                 - b.healthDropsCollected * 30f
                 - b.damageTaken * 1f
                 - Mathf.Max(0f, b.time - parTime2) * TIME_WEIGHT_2
                 + (b.perfect ? 250f : 0f);

        float l3 = (c.bossDefeated ? 500f : (1f - c.bossHealthFinal) * 400f)
                 + (c.reachedMoonPhase ? 150f : 0f)
                 + c.piecesDelivered * 30f
                 + c.blocks * 10f
                 - c.damageTaken * 1f
                 - c.jammoDamageTaken * 1f
                 - c.piecesLost * 25f
                 - Mathf.Max(0f, c.time - parTime3) * TIME_WEIGHT_3
                 + (c.perfect ? 250f : 0f);

        float bonus = (g.perfectHealth ? 500f : 0f) + g.accuracyGlobal * 200f;

        Current.level1Score = Mathf.RoundToInt(l1);
        Current.level2Score = Mathf.RoundToInt(l2);
        Current.level3Score = Mathf.RoundToInt(l3);
        Current.bonus = Mathf.RoundToInt(bonus);
        Current.finalScore = Mathf.Max(0, Mathf.RoundToInt(l1 + l2 + l3 + bonus));
        Current.grade = GradeFor(Current.finalScore);
    }

    /// <summary>Maps a final score to a synthetic grade (S/A/B/C/D).</summary>
    public static string GradeFor(int finalScore)
    {
        if (finalScore >= GRADE_S) return "S";
        if (finalScore >= GRADE_A) return "A";
        if (finalScore >= GRADE_B) return "B";
        if (finalScore >= GRADE_C) return "C";
        return "D";
    }

    // ---- Persistence ------------------------------------------------------

    /// <summary>
    /// Computes the scores and writes a leaderboard entry for <paramref name="playerName"/>. The entry
    /// stores a DEEP COPY of the current stats (via JSON), so it is not affected by a later
    /// <see cref="ResetRun"/> or further mutations.
    /// </summary>
    public LeaderboardEntry SaveEntry(string playerName)
    {
        ComputeScores();

        RunStats snapshot = JsonUtility.FromJson<RunStats>(JsonUtility.ToJson(Current));
        LeaderboardEntry entry = new LeaderboardEntry
        {
            playerName = string.IsNullOrWhiteSpace(playerName) ? "Anonymous" : playerName.Trim(),
            grade = Current.grade,
            finalScore = Current.finalScore,
            stats = snapshot,
            dateIso = DateTime.UtcNow.ToString("o")
        };
        LeaderboardStore.Add(entry);
        return entry;
    }

    /// <summary>Starts a fresh run (new stats); call at the start of a new playthrough.</summary>
    public void ResetRun()
    {
        Current = new RunStats();
        _activeTimerLevel = 0;
        _currentStreak = 0;
    }

    // ---- Debug (no scene wiring needed) -----------------------------------

    /// <summary>Fills <see cref="Current"/> with a deterministic sample run and computes its score (for testing).</summary>
    [ContextMenu("Debug: Fill sample run")]
    public void DebugFillSampleRun()
    {
        ResetRun();
        Current.l1.completed = true; Current.l1.fallen = 0; Current.l1.helpNeeded = 0; Current.l1.time = 50f;
        Current.l2.enemiesKilled = 20; Current.l2.wavesCleared = 5; Current.l2.statuePartsPlaced = 5;
        Current.l2.statueCompleted = true; Current.l2.bestKillStreak = 10; Current.l2.blocks = 8;
        Current.l2.healthDropsSpawned = 4; Current.l2.healthDropsCollected = 0; Current.l2.damageTaken = 0f; Current.l2.time = 100f;
        Current.l3.bossDefeated = true; Current.l3.bossHealthFinal = 0f; Current.l3.reachedMoonPhase = true;
        Current.l3.piecesDelivered = 5; Current.l3.piecesLost = 0; Current.l3.blocks = 6;
        Current.l3.damageTaken = 0f; Current.l3.jammoDamageTaken = 0f; Current.l3.time = 150f;
        Current.global.totalSwings = 40; Current.global.totalHits = 34;
        Current.global.totalDamageDealt = 1500f; Current.global.totalEnemiesKilled = 21;

        ComputeScores();
        Debug.Log($"[ScoreManager] Sample run: final={Current.finalScore} grade={Current.grade} " +
                  $"(L1={Current.level1Score} L2={Current.level2Score} L3={Current.level3Score} bonus={Current.bonus})", this);
    }

    /// <summary>Saves the current run as "TEST" and logs the whole leaderboard (verifies persistence round-trip).</summary>
    [ContextMenu("Debug: Save sample + dump leaderboard")]
    public void DebugSaveAndDump()
    {
        LeaderboardEntry e = SaveEntry("TEST");
        Debug.Log($"[ScoreManager] Saved: {e.playerName} {e.finalScore} ({e.grade})", this);

        LeaderboardData data = LeaderboardStore.Load();
        for (int i = 0; i < data.entries.Count; i++)
        {
            LeaderboardEntry x = data.entries[i];
            Debug.Log($"  #{i + 1} {x.playerName} {x.finalScore} ({x.grade}) | " +
                      $"L1={x.stats.level1Score} L2={x.stats.level2Score} L3={x.stats.level3Score} " +
                      $"acc={x.stats.global.accuracyGlobal:0.00} date={x.dateIso}", this);
        }
    }

    /// <summary>Clears the saved leaderboard (debug).</summary>
    [ContextMenu("Debug: Clear leaderboard")]
    public void DebugClearLeaderboard() => LeaderboardStore.Clear();

    /// <summary>
    /// Writes three varied sample entries to the leaderboard (debug) so the UI can be populated and
    /// previewed without a full playthrough: ALEX (near-flawless ≈ S), JAMIE (solid, took damage ≈ B),
    /// SAM (died in the duel, partial credit ≈ D). Resets the run afterwards.
    /// </summary>
    [ContextMenu("Debug: Seed sample leaderboard")]
    public void DebugSeedSampleEntries()
    {
        // ALEX — near-flawless run.
        ResetRun();
        Current.l1.completed = true; Current.l1.time = 48f;
        Current.l2.enemiesKilled = 22; Current.l2.wavesCleared = 5; Current.l2.statuePartsPlaced = 5;
        Current.l2.statueCompleted = true; Current.l2.bestKillStreak = 12; Current.l2.blocks = 9;
        Current.l2.healthDropsSpawned = 5; Current.l2.time = 95f;
        Current.l3.bossDefeated = true; Current.l3.reachedMoonPhase = true; Current.l3.piecesDelivered = 5;
        Current.l3.blocks = 7; Current.l3.time = 140f;
        Current.global.totalSwings = 50; Current.global.totalHits = 44;
        SaveEntry("ALEX");

        // JAMIE — solid clear, took damage, missed perfects.
        ResetRun();
        Current.l1.completed = true; Current.l1.time = 70f; Current.l1.helpNeeded = 1;
        Current.l2.enemiesKilled = 16; Current.l2.wavesCleared = 4; Current.l2.statuePartsPlaced = 5;
        Current.l2.statueCompleted = true; Current.l2.bestKillStreak = 6; Current.l2.blocks = 4;
        Current.l2.healthDropsSpawned = 3; Current.l2.healthDropsCollected = 2; Current.l2.damageTaken = 60f; Current.l2.time = 130f;
        Current.l3.bossDefeated = true; Current.l3.reachedMoonPhase = true; Current.l3.piecesDelivered = 5;
        Current.l3.piecesLost = 1; Current.l3.blocks = 3; Current.l3.damageTaken = 45f; Current.l3.jammoDamageTaken = 20f; Current.l3.time = 200f;
        Current.global.totalSwings = 70; Current.global.totalHits = 40;
        SaveEntry("JAMIE");

        // SAM — died in the Moon phase, partial credit.
        ResetRun();
        Current.l1.completed = true; Current.l1.time = 90f; Current.l1.fallen = 3; Current.l1.helpNeeded = 3;
        Current.l2.enemiesKilled = 10; Current.l2.wavesCleared = 3; Current.l2.statuePartsPlaced = 5;
        Current.l2.statueCompleted = true; Current.l2.damageTaken = 90f; Current.l2.time = 160f;
        Current.l3.bossDefeated = false; Current.l3.bossHealthFinal = 0.6f; Current.l3.reachedMoonPhase = true;
        Current.l3.piecesDelivered = 3; Current.l3.piecesLost = 2; Current.l3.damageTaken = 100f; Current.l3.time = 150f;
        Current.global.totalSwings = 60; Current.global.totalHits = 22;
        SaveEntry("SAM");

        ResetRun();
        Debug.Log("[ScoreManager] Seeded 3 sample leaderboard entries (ALEX/JAMIE/SAM).", this);
    }
}
