using UnityEngine;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Persistent singleton manager handling session statistics, scoring algorithms,
/// and local JSON serialization for the global leaderboard.
/// </summary>
public class DataManager : MonoBehaviour
{
    public static DataManager instance;

    [Header("Current Match Data")]
    public PlayerRecord currentMatchData; 

    [Header("Global Leaderboard")]
    public LeaderboardData leaderboard = new LeaderboardData();

    private string saveFilePath;

    /// <summary>
    /// Initializes the singleton instance, ensures cross-scene persistence, 
    /// defines the target JSON file path, and preloads existing leaderboard data.
    /// </summary>
    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            saveFilePath = Application.persistentDataPath + "/leaderboard.json";
            LoadLeaderboard();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Validates if a specific player name already exists in the cached leaderboard array.
    /// Performs case-insensitive and whitespace-agnostic comparisons to prevent duplicates.
    /// </summary>
    /// <param name="nameToCheck">The string ID to query against existing records.</param>
    /// <returns>True if a matching record is found, otherwise false.</returns>
    public bool CheckIfNameExists(string nameToCheck)
    {
        string lowerName = nameToCheck.ToLower().Trim();
        foreach (PlayerRecord record in leaderboard.records)
        {
            if (record.playerName.ToLower().Trim() == lowerName)
            {
                return true; 
            }
        }
        return false;
    }

    /// <summary>
    /// Instantiates a fresh performance tracking object for the incoming gameplay session.
    /// </summary>
    /// <param name="chosenName">The registered player identifier for this session.</param>
    public void StartNewMatch(string chosenName)
    {
        currentMatchData = new PlayerRecord();
        currentMatchData.playerName = chosenName;
        currentMatchData.timeTaken = 0f;
    }

    /// <summary>
    /// Processes end-of-match statistics, executes the deterministic scoring algorithm,
    /// evaluates high-score overrides, and triggers local JSON serialization.
    /// </summary>
    public void FinalizeAndSaveMatch()
    {
        PlayerHealth healthScript = Object.FindFirstObjectByType<PlayerHealth>();
        if (healthScript != null)
        {
            currentMatchData.potionsCollected = healthScript.potionsCount;
        }

        int score = 0;
        score += currentMatchData.enemiesDefeated * 500;
        score += currentMatchData.perfectParries * 300;
        score += currentMatchData.damageDealt * 1;
        score += currentMatchData.damageBlocked * 2;
        score -= currentMatchData.normalDefenses * 20;
        score += currentMatchData.potionsCollected * 150; 

        int timeBonus = 5000 - (int)(currentMatchData.timeTaken * 10);
        if (timeBonus > 0) score += timeBonus;
        if (currentMatchData.healthLost == 0) score += 2000;

        if (score < 0) score = 0;
        currentMatchData.finalScore = score;

        PlayerRecord existingRecord = leaderboard.records.Find(r => 
            r.playerName.ToLower().Trim() == currentMatchData.playerName.ToLower().Trim()
        );

        if (existingRecord != null)
        {
            if (currentMatchData.finalScore > existingRecord.finalScore)
            {
                Debug.Log("New Personal Best! Updating data for: " + currentMatchData.playerName);
                
                existingRecord.finalScore = currentMatchData.finalScore;
                existingRecord.enemiesDefeated = currentMatchData.enemiesDefeated;
                existingRecord.perfectParries = currentMatchData.perfectParries;
                existingRecord.damageDealt = currentMatchData.damageDealt;
                existingRecord.damageBlocked = currentMatchData.damageBlocked;
                existingRecord.healthLost = currentMatchData.healthLost;
                existingRecord.normalDefenses = currentMatchData.normalDefenses;
                existingRecord.timeTaken = currentMatchData.timeTaken;
                existingRecord.potionsCollected = currentMatchData.potionsCollected;
            }
            else
            {
                Debug.Log("Score is lower than previous record. Bypassing save.");
            }
        }
        else
        {
            Debug.Log("First entry registered for: " + currentMatchData.playerName);
            leaderboard.records.Add(currentMatchData);
        }

        leaderboard.records.Sort(); 
        SaveLeaderboard();
    }

    /// <summary>
    /// Serializes the current leaderboard state into a formatted JSON string 
    /// and writes it to the local persistent data directory.
    /// </summary>
    private void SaveLeaderboard()
    {
        string json = JsonUtility.ToJson(leaderboard, true);
        File.WriteAllText(saveFilePath, json);
        Debug.Log("Leaderboard saved to: " + saveFilePath);
    }

    /// <summary>
    /// Reads and deserializes the JSON leaderboard file back into runtime memory.
    /// Bypasses the load operation safely if no local save file exists.
    /// </summary>
    private void LoadLeaderboard()
    {
        if (File.Exists(saveFilePath))
        {
            string json = File.ReadAllText(saveFilePath);
            leaderboard = JsonUtility.FromJson<LeaderboardData>(json);
        }
    }
}