using UnityEngine;

/// <summary>
/// Specialized trigger hitbox used specifically during the Boss's 'Crazy Mode' phase.
/// Detects overlapping player colliders and applies high-magnitude damage.
/// </summary>
public class Enemy2CrazyHitbox : MonoBehaviour
{
    [Tooltip("The amount of damage dealt to the player during this specific phase attack.")]
    public int damage = 10; 

    /// <summary>
    /// Executes when a 2D collider enters the trigger zone. 
    /// Filters for the 'Player' tag before accessing the health system.
    /// </summary>
    /// <param name="collision">The other Collider2D involved in this trigger event.</param>
    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Check if the entity entering the hit zone is the player chararacter
        if (collision.CompareTag("Player"))
        {
            PlayerHealth playerHealth = collision.GetComponent<PlayerHealth>();
            
            if (playerHealth != null)
            {
                // Deliver the phase-specific damage amount to the player
                playerHealth.TakeDamage(damage);
            }
        }
    }
}