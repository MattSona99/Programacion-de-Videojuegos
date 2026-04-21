using UnityEngine;

/// <summary>
/// Handles arena-specific initialization logic, such as synchronizing player 
/// positioning with designated spawn locations upon level instantiation.
/// </summary>
public class ArenaManager : MonoBehaviour
{
    [Header("Spawn Configuration")]
    [Tooltip("The Transform marker defining the player's starting coordinates on the left side of the arena.")]
    public Transform leftSpawnPoint; 

    /// <summary>
    /// Executes initial scene setup. Searches for the player entity and performs 
    /// a position sync with the arena's spawn point.
    /// </summary>
    void Start()
    {
        // Locate the player character within the scene hierarchy via tag.
        // While generally expensive, this is performed once during the initialization phase.
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        // Validate player existence before attempting transform manipulation to prevent null errors.
        if (player != null)
        {
            // Update the player's world position to match the pre-defined spawn anchor.
            player.transform.position = leftSpawnPoint.position;
        }
        else
        {
            // Provide developer feedback in the console if the scene setup is missing a tagged player object.
            Debug.LogWarning("Warning: Player not found! Make sure the GameObject has the 'Player' tag.");
        }
    }
}