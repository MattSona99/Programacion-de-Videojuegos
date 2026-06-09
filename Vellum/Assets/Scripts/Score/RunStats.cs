using System;

/// <summary>
/// Global, cross-level statistics for a single playthrough. Plain <see cref="SerializableAttribute"/>
/// container so <see cref="UnityEngine.JsonUtility"/> can persist it inside a leaderboard entry.
/// Totals are accumulated by <see cref="ScoreManager"/> as the run progresses.
/// </summary>
[Serializable]
public class GlobalStats
{
    public float totalTime;            // sum of the three level times (effective, frozen on pause)
    public float totalDamageDealt;     // total damage the Player dealt to enemies/boss
    public float totalDamageTaken;     // total damage the Player took across the run
    public int totalEnemiesKilled;     // L2 kills + the L3 boss (if defeated)
    public int totalSwings;            // melee swings started (Accuracy denominator)
    public int totalHits;              // melee swings that landed on a target (Accuracy numerator)
    public float completionPercent;    // levels completed / 3 (0..1)
    public float accuracyGlobal;       // hits / swings (0..1); filled in ComputeScores for the snapshot
    public bool perfectHealth;         // no damage taken in the whole run

    /// <summary>Hits / swings as a 0..1 fraction (0 when no swing was made). Use for live reads.</summary>
    public float AccuracyGlobal => totalSwings > 0 ? (float)totalHits / totalSwings : 0f;
}

/// <summary>
/// Level 1 (Path Puzzle) statistics. The level has no death/fail-end, so it is always completed
/// when scored; it is graded as a completion base plus quality modifiers (see <c>docs/punteggio.md</c>).
/// </summary>
[Serializable]
public class LevelOneStats
{
    public bool completed;
    public int fallen;                 // FailAndReset count (wrong steps)
    public int helpNeeded;             // memory-hint uses
    public float time;                 // start → puzzle completed
    public float timeToCheckpoint;     // [stat] split
    public float timeCheckpointToEnd;  // [stat] split
    public bool noHintClear;           // helpNeeded == 0
    public bool perfect;               // completed && fallen == 0 && helpNeeded == 0
}

/// <summary>Level 2 (Arena + Statue) statistics.</summary>
[Serializable]
public class LevelTwoStats
{
    public int enemiesKilled;
    public int wavesCleared;
    public int statuePartsPlaced;
    public bool statueCompleted;
    public int bestKillStreak;         // kills without taking damage
    public int blocks;                 // shield blocks/parries
    public int healthDropsSpawned;
    public int healthDropsCollected;
    public float damageTaken;
    public float time;                 // arena start → statue complete
    public bool perfect;               // statueCompleted && damageTaken == 0
}

/// <summary>Level 3 (Mirror Duel) statistics. Supports partial credit when the run ends in defeat.</summary>
[Serializable]
public class LevelThreeStats
{
    public bool bossDefeated;
    public float bossHealthFinal;      // bossHealth.Normalized at the end (0..1), for partial credit
    public bool reachedMoonPhase;
    public int piecesDelivered;        // summed over both phases
    public int piecesLost;             // Moon phase: Jammo hit while carrying
    public int blocks;
    public float damageTaken;          // Player
    public float jammoDamageTaken;
    public float time;                 // duel start → win
    public bool perfect;               // bossDefeated && damageTaken == 0 && piecesLost == 0
}

/// <summary>
/// Full snapshot of one playthrough: the per-level and global stats plus the computed scores
/// (<see cref="ScoreManager.ComputeScores"/>). Stored verbatim in each <see cref="LeaderboardEntry"/>
/// so every entry carries its complete breakdown.
/// </summary>
[Serializable]
public class RunStats
{
    public GlobalStats global = new GlobalStats();
    public LevelOneStats l1 = new LevelOneStats();
    public LevelTwoStats l2 = new LevelTwoStats();
    public LevelThreeStats l3 = new LevelThreeStats();

    // Computed scores (filled by ComputeScores).
    public int level1Score;
    public int level2Score;
    public int level3Score;
    public int bonus;
    public int finalScore;
    public string grade;
}
