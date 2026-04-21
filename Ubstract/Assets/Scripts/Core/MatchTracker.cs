using UnityEngine;

/// <summary>
/// Component responsible for tracking real-time gameplay metrics and relaying 
/// them to the DataManager. Acts as a bridge between active game events 
/// (combat, movement, timing) and the persistent data profile.
/// </summary>
public class MatchTracker : MonoBehaviour
{
    /// <summary>
    /// Updates the total elapsed time for the current session.
    /// Uses DeltaTime to ensure frame-rate independent time tracking.
    /// </summary>
    void Update()
    {
        if (DataManager.instance != null && DataManager.instance.currentMatchData != null)
        {
            DataManager.instance.currentMatchData.timeTaken += Time.deltaTime;
        }
    }

    /// <summary>
    /// Increments the kill counter in the current match data.
    /// Safety checks ensure the DataManager instance is valid before access.
    /// </summary>
    public void AddEnemyDefeated() 
    { 
        if (DataManager.instance != null && DataManager.instance.currentMatchData != null)
            DataManager.instance.currentMatchData.enemiesDefeated++; 
    }

    /// <summary>
    /// Registers a frame-perfect parry event for score calculation.
    /// </summary>
    public void AddPerfectParry() 
    { 
        if (DataManager.instance != null && DataManager.instance.currentMatchData != null)
            DataManager.instance.currentMatchData.perfectParries++; 
    }

    /// <summary>
    /// Tracks standard blocks/defenses that do not qualify as perfect parries.
    /// </summary>
    public void AddNormalDefense() 
    { 
        if (DataManager.instance != null && DataManager.instance.currentMatchData != null)
            DataManager.instance.currentMatchData.normalDefenses++; 
    }

    /// <summary>
    /// Accumulates raw damage values dealt to enemies.
    /// </summary>
    /// <param name="amount">The integer amount of damage inflicted this hit.</param>
    public void AddDamageDealt(int amount) 
    { 
        if (DataManager.instance != null && DataManager.instance.currentMatchData != null)
            DataManager.instance.currentMatchData.damageDealt += amount; 
    }

    /// <summary>
    /// Records total health lost by the player for final ranking penalties or bonuses.
    /// </summary>
    /// <param name="amount">The amount of health subtracted from the player.</param>
    public void AddHealthLost(int amount) 
    { 
        if (DataManager.instance != null && DataManager.instance.currentMatchData != null)
            DataManager.instance.currentMatchData.healthLost += amount; 
    }

    /// <summary>
    /// Records the amount of potential damage mitigated through successful blocking.
    /// </summary>
    /// <param name="amount">The amount of damage prevented.</param>
    public void AddDamageBlocked(int amount) 
    { 
        if (DataManager.instance != null && DataManager.instance.currentMatchData != null)
            DataManager.instance.currentMatchData.damageBlocked += amount; 
    }
}