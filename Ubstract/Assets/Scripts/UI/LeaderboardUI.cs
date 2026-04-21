using UnityEngine;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Controls the Leaderboard UI panel, managing the dynamic population of 
/// player records within a scrollable list and ensuring data synchronization 
/// with local storage.
/// </summary>
public class LeaderboardUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The parent transform (usually a Vertical Layout Group) where record entries will be spawned.")]
    public Transform contentArea; 
    
    [Tooltip("The visual prefab representing a single player's rank and score summary.")]
    public GameObject entryPrefab; 
    
    /// <summary>
    /// Lifecycle event triggered whenever the UI panel is enabled.
    /// Ensures the leaderboard display is up-to-date with the latest save data.
    /// </summary>
    void OnEnable()
    {
        RefreshLeaderboard();
    }

    /// <summary>
    /// Clears the current list, retrieves the latest high scores from DataManager or local JSON, 
    /// sorts them by rank, and instantiates the UI entry blocks.
    /// </summary>
    public void RefreshLeaderboard()
    {
        // Phase 1: Hierarchy Cleanup
        // Destroy existing entries to prevent duplicates when reopening the menu
        foreach (Transform child in contentArea)
        {
            Destroy(child.gameObject);
        }

        // Phase 2: Data Acquisition
        // Prioritize singleton instance data, with a secondary fallback to direct disk I/O
        List<PlayerRecord> recordsToDisplay = new List<PlayerRecord>();

        if (DataManager.instance != null && DataManager.instance.leaderboard != null)
        {
            recordsToDisplay = DataManager.instance.leaderboard.records;
        }
        else
        {
            // Fallback: Manually read and deserialize the JSON file if the manager is unavailable
            string path = Application.persistentDataPath + "/leaderboard.json";
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                LeaderboardData data = JsonUtility.FromJson<LeaderboardData>(json);
                if (data != null) recordsToDisplay = data.records;
            }
        }

        // Phase 3: Validation
        if (recordsToDisplay == null || recordsToDisplay.Count == 0)
        {
            Debug.Log("Leaderboard is empty. No records to display.");
            return;
        }

        // Phase 4: Sorting
        // Executes the IComparable logic defined in PlayerRecord to order scores descending
        recordsToDisplay.Sort();

        // Phase 5: Dynamic UI Generation
        for (int i = 0; i < recordsToDisplay.Count; i++)
        {
            // Instantiate the record prefab as a child of the scroll view content area
            GameObject newObj = Instantiate(entryPrefab, contentArea);
            
            // Link the data record to the UI entry controller for visual setup
            LeaderboardEntry entryScript = newObj.GetComponent<LeaderboardEntry>();
            if (entryScript != null)
            {
                // Passing i + 1 as the rank to convert zero-based index to human-readable ranking
                entryScript.Setup(i + 1, recordsToDisplay[i]); 
            }
        }
    }
}