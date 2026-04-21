using System;

/// <summary>
/// Serializable data model representing a single player's performance metrics.
/// Implements IComparable to facilitate automated sorting within the leaderboard collection.
/// </summary>
[System.Serializable]
public class PlayerRecord : IComparable<PlayerRecord>
{
    public string playerName;
    public int finalScore;
    
    [UnityEngine.Header("Detailed Statistics")]
    public int enemiesDefeated;
    public int perfectParries;
    public int normalDefenses;
    public int damageDealt;
    public int healthLost;
    public int damageBlocked;
    public int potionsCollected;
    public float timeTaken;

    /// <summary>
    /// Custom comparison logic used by Sort algorithms. 
    /// Organizes records in descending order (highest score first).
    /// </summary>
    /// <param name="other">The other PlayerRecord instance to compare against.</param>
    /// <returns>A value indicating the relative order of the objects being compared.</returns>
    public int CompareTo(PlayerRecord other)
    {
        return other.finalScore.CompareTo(this.finalScore);
    }
}

/// <summary>
/// Wrapper class used for JSON serialization. 
/// Unity's JsonUtility requires a top-level object to serialize generic lists.
/// </summary>
[System.Serializable]
public class LeaderboardData
{
    public System.Collections.Generic.List<PlayerRecord> records = 
    new System.Collections.Generic.List<PlayerRecord>();
}