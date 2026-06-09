using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One leaderboard record: the player's name, the synthetic grade, the numeric final score, and the
/// FULL <see cref="RunStats"/> breakdown (global + per level), plus an ISO timestamp. Serializable so
/// <see cref="UnityEngine.JsonUtility"/> can round-trip it through <see cref="LeaderboardStore"/>.
/// </summary>
[Serializable]
public class LeaderboardEntry
{
    public string playerName;
    public string grade;
    public int finalScore;
    public RunStats stats;
    public string dateIso;
}

/// <summary>
/// Wrapper holding the list of entries. <see cref="UnityEngine.JsonUtility"/> cannot serialize a
/// top-level <see cref="List{T}"/>, so the list must live inside a serializable object.
/// </summary>
[Serializable]
public class LeaderboardData
{
    public List<LeaderboardEntry> entries = new List<LeaderboardEntry>();
}

/// <summary>
/// Local persistence for the leaderboard, backed by <see cref="PlayerPrefs"/> (a single JSON string).
/// Pure static utility — no scene object required. Entries are kept sorted by descending
/// <see cref="LeaderboardEntry.finalScore"/> and trimmed to a top-N.
/// </summary>
public static class LeaderboardStore
{
    /// <summary>PlayerPrefs key under which the serialized <see cref="LeaderboardData"/> is stored.</summary>
    public const string PrefsKey = "vellum.leaderboard";

    /// <summary>Default number of entries kept after trimming.</summary>
    public const int DefaultMaxEntries = 10;

    /// <summary>Loads the saved leaderboard, or an empty one if absent/corrupted.</summary>
    public static LeaderboardData Load()
    {
        string json = PlayerPrefs.GetString(PrefsKey, string.Empty);
        if (string.IsNullOrEmpty(json)) return new LeaderboardData();

        try
        {
            LeaderboardData data = JsonUtility.FromJson<LeaderboardData>(json);
            if (data == null) return new LeaderboardData();
            if (data.entries == null) data.entries = new List<LeaderboardEntry>();
            return data;
        }
        catch (Exception e)
        {
            // Corrupted/old format: start fresh rather than break the game.
            Debug.LogWarning($"[LeaderboardStore] Failed to parse saved leaderboard, resetting it: {e.Message}");
            return new LeaderboardData();
        }
    }

    /// <summary>Serializes and persists the leaderboard to <see cref="PlayerPrefs"/>.</summary>
    public static void Save(LeaderboardData data)
    {
        if (data == null) return;
        PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Inserts <paramref name="entry"/>, sorts by descending final score, trims to
    /// <paramref name="maxEntries"/>, persists, and returns the updated leaderboard.
    /// </summary>
    public static LeaderboardData Add(LeaderboardEntry entry, int maxEntries = DefaultMaxEntries)
    {
        LeaderboardData data = Load();
        if (entry != null) data.entries.Add(entry);

        data.entries.Sort((a, b) => b.finalScore.CompareTo(a.finalScore));
        if (maxEntries > 0 && data.entries.Count > maxEntries)
            data.entries.RemoveRange(maxEntries, data.entries.Count - maxEntries);

        Save(data);
        return data;
    }

    /// <summary>Clears the saved leaderboard (debug / reset).</summary>
    public static void Clear()
    {
        PlayerPrefs.DeleteKey(PrefsKey);
        PlayerPrefs.Save();
    }
}
