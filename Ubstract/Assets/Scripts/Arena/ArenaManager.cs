using UnityEngine;

/// <summary>
/// Handles arena-specific initialization logic, synchronizing player positioning.
/// Transitions and freezing are now safely handled globally by the GameManager.
/// </summary>
public class ArenaManager : MonoBehaviour
{
    [Header("Spawn Configuration")]
    [Tooltip("The Transform marker defining the player's starting coordinates on the left side of the arena.")]
    public Transform leftSpawnPoint; 

    void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player != null && leftSpawnPoint != null)
        {
            player.transform.position = leftSpawnPoint.position;
        }
        else
        {
            Debug.LogWarning("ArenaManager: Player or Spawn Point non trovato!");
        }
    }
}