using UnityEngine;
using System.Collections;

/// <summary>
/// Logic for a collectible health item. Implements a brief cooldown upon spawning 
/// to allow for physical "dropping" behavior before the player can trigger the recovery.
/// </summary>
public class HealthPickup : MonoBehaviour
{
    [Tooltip("The amount of HP restored to the player upon collection.")]
    public int healAmount = 5; 
    
    [Tooltip("The safety delay (in seconds) before the item becomes interactive, allowing it to settle in the environment.")]
    public float pickupDelay = 0.5f;

    private bool canBePickedUp = false;

    /// <summary>
    /// Initializes the pickup delay timer as soon as the object is instantiated.
    /// </summary>
    void Start()
    {
        StartCoroutine(EnablePickupRoutine());
    }

    /// <summary>
    /// Coroutine that manages the transition from a non-interactive physics object 
    /// to an active collectible item.
    /// </summary>
    private IEnumerator EnablePickupRoutine()
    {
        yield return new WaitForSeconds(pickupDelay);
        canBePickedUp = true;
    }

    /// <summary>
    /// Handles the physics-based interaction. If the collision involves the Player 
    /// and the item is active, it triggers the healing logic and disposes of the object.
    /// </summary>
    /// <param name="collision">The collision data for the current physics event.</param>
    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Guard clause: bypass logic if the pickup delay hasn't expired
        if (!canBePickedUp) return;

        // Check if the colliding entity is the player via tag comparison
        if (collision.gameObject.CompareTag("Player"))
        {
            PlayerHealth ph = collision.gameObject.GetComponent<PlayerHealth>();
            if (ph != null)
            {
                // Execute recovery logic on the player's health system
                ph.Heal(healAmount);
                
                // Cleanup the pickup object from the scene
                Destroy(gameObject);
            }
        }
    }
}