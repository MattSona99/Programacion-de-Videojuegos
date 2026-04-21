using UnityEngine;

/// <summary>
/// Dynamic hitbox handler for Boss 2's combo sequences.
/// Uses stay-based trigger detection to ensure hits register even if the player 
/// is already within the radius when damage frames are activated via Animation Events.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class Enemy2ComboHitbox : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("Reference to the main enemy controller containing the current damage and attack state.")]
    public EnemyAI_2 enemyScript;

    /// <summary>
    /// Continuously checks for player presence within the trigger zone.
    /// Damage is processed only when the enemyScript's currentComboDamage is non-zero,
    /// enabling precise synchronization with pixel-art impact frames.
    /// </summary>
    /// <param name="collision">The Collider2D entering or staying within the trigger.</param>
    private void OnTriggerStay2D(Collider2D collision)
    {
        // Execute damage logic only if colliding with the player and active damage frames are triggered
        if (collision.CompareTag("Player") && enemyScript.currentComboDamage > 0)
        {
            PlayerHealth playerHealth = collision.GetComponent<PlayerHealth>();
            PlayerMovements playerMovements = collision.GetComponent<PlayerMovements>();

            if (playerHealth != null)
            {
                // Logic for Attack 5: Earthquake / Ground Slam (Unblockable)
                if (enemyScript.isCurrentHitUnblockable)
                {
                    Debug.Log("Earthquake hitbox active. Player grounded status: " + (playerMovements != null ? playerMovements.isGrounded.ToString() : "Null"));
                    
                    // Unblockable damage applies only if the player is touching the ground
                    if (playerMovements != null && playerMovements.isGrounded)
                    {
                        // Player is grounded: Bypass normal parry/block logic
                        playerHealth.TakeUnblockableDamage(enemyScript.currentComboDamage);
                        
                        // Immediately reset damage to 0 to prevent multi-hit registration within the same frame
                        enemyScript.AnimEvent_ResetHit(); 
                    }
                    else
                    {
                        Debug.Log("Player avoided shockwave by being airborne.");
                    }
                }
                // Logic for Attacks 1 through 4: Standard Melee Strikes
                else
                {
                    // Pass damage through the standard parry/defense system
                    playerHealth.TakeDamage(enemyScript.currentComboDamage);
                    
                    // Immediately reset damage to 0 to prevent multi-hit registration
                    enemyScript.AnimEvent_ResetHit(); 
                }
            }
        }
    }
}