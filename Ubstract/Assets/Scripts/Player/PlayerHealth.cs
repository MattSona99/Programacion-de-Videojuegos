using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the player's health system, UI synchronization, and damage mitigation logic.
/// Handles perfect parries with damage reflection and integrates with the MatchTracker for scoring.
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    public int potionsCount = 0;

    [Header("Vitality Stats")]
    public int maxHealth = 100;
    private int currentHealth;

    [Header("Health Bar UI")]
    public Image healthBarFill;
    public Gradient healthGradient;

    [Header("Smooth UI Interpolation")]
    [Tooltip("The speed at which the visual health bar catches up to the actual health value.")]
    public float lerpSpeed = 5f; 
    private float visualHealth;  

    [Header("Defense & Parry Mechanics")]
    [Tooltip("The time window (in seconds) after pressing block during which a parry is considered 'Perfect'.")]
    public float parryWindow = 0.2f;
    private float parryTimer = 0f;

    private PlayerCombat combatScript;
    private MatchTracker matchTracker;

    [Header("Visual Feedback")]
    [Tooltip("Prefab for spawning damage numbers and combat status text.")]
    public FloatingText floatingTextPrefab;

    [Header("Character Identity")]
    [Tooltip("UI element used to display the player's profile name.")]
    public TMPro.TextMeshProUGUI nameDisplayText;

    /// <summary>
    /// Initializes health values, caches component references, and retrieves session data from the DataManager.
    /// </summary>
    void Start()
    {
        currentHealth = maxHealth;
        visualHealth = maxHealth; 

        combatScript = GetComponent<PlayerCombat>();
        matchTracker = Object.FindFirstObjectByType<MatchTracker>();

        UpdateUI();

        if (nameDisplayText != null)
        {
            if (DataManager.instance != null && 
                DataManager.instance.currentMatchData != null && 
                !string.IsNullOrEmpty(DataManager.instance.currentMatchData.playerName))
            {
                nameDisplayText.text = DataManager.instance.currentMatchData.playerName;
            }
            else
            {
                nameDisplayText.text = "Hero"; 
            }
        }
    }

    /// <summary>
    /// Monitors parry timing and smooths the health bar UI transition using linear interpolation.
    /// </summary>
    void Update()
    {
        if (Input.GetMouseButtonDown(1)) 
        {
            parryTimer = parryWindow;
        }

        if (parryTimer > 0)
        {
            parryTimer -= Time.deltaTime;
        }

        if (Mathf.Abs(visualHealth - currentHealth) > 0.01f)
        {
            visualHealth = Mathf.Lerp(visualHealth, currentHealth, Time.deltaTime * lerpSpeed);
            UpdateUI(); 
        }
    }

    /// <summary>
    /// Processes incoming damage. Applies mitigation if the player is defending and triggers 
    /// a 'Perfect Parry' reflection if the timing window is active.
    /// </summary>
    /// <param name="damageAmount">The raw damage value to process.</param>
    public void TakeDamage(int damageAmount)
    {
        if (combatScript != null && combatScript.isDefending)
        {
            if (parryTimer > 0)
            {
                if (floatingTextPrefab) 
                {
                    FloatingText text = Instantiate(floatingTextPrefab, transform.position, Quaternion.identity);
                    text.Setup("PERFECT PARRY!", Color.cyan);
                }
                
                if (matchTracker != null) matchTracker.AddPerfectParry();

                // Reflect double the incoming damage back to the attacker
                combatScript.ReflectDamage(damageAmount * 2);
                parryTimer = 0f; 
                return; 
            }
            else
            {
                if (floatingTextPrefab) 
                {
                    FloatingText text = Instantiate(floatingTextPrefab, transform.position, Quaternion.identity);
                    text.Setup("DEFENDED!", Color.yellow);
                    PlayerAudio pAudio = GetComponent<PlayerAudio>();
                    if (pAudio != null)
                    {
                        pAudio.PlaySound(pAudio.parrySound);
                    }
                }
                
                // Mitigate damage by 50% during standard blocks
                int blockedAmount = damageAmount - (damageAmount / 2);
                if (matchTracker != null)
                {
                    matchTracker.AddNormalDefense();
                    matchTracker.AddDamageBlocked(blockedAmount);
                }

                damageAmount = damageAmount / 2;
            }
        }

        if (matchTracker != null) matchTracker.AddHealthLost(damageAmount);

        currentHealth -= damageAmount;

        if (floatingTextPrefab) 
        {
            FloatingText text = Instantiate(floatingTextPrefab, transform.position, Quaternion.identity);
            text.Setup("-" + damageAmount.ToString(), Color.green); 
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// Special damage handler for ground-based shockwaves. 
    /// Damage is bypassed if the player character is currently in the air.
    /// </summary>
    /// <param name="damage">The damage value to apply if grounded.</param>
    public void TakeEarthquakeDamage(int damage)
    {
        PlayerMovements moveScript = GetComponent<PlayerMovements>();
        
        if (moveScript != null)
        {
            if (moveScript.isGrounded == true)
            {
                Debug.Log("Impacted by shockwave!");
                TakeDamage(damage); 
            }
            else
            {
                Debug.Log("Shockwave evaded via jump!");
            }
        }
    }

    /// <summary>
    /// Processes critical or environmental damage that cannot be blocked or parried.
    /// </summary>
    /// <param name="damageAmount">The absolute damage value to subtract.</param>
    public void TakeUnblockableDamage(int damageAmount)
    {
        if (matchTracker != null) matchTracker.AddHealthLost(damageAmount);

        currentHealth -= damageAmount;

        if (floatingTextPrefab)
        {
            FloatingText text = Instantiate(floatingTextPrefab, transform.position, Quaternion.identity);
            text.Setup("CRITICAL! -" + damageAmount.ToString(), Color.red);
        }

        if (currentHealth <= 0)
        {
            Die();
        }

        UpdateUI();
    }

    /// <summary>
    /// Restores a specified amount of HP and increments the potion collection counter for session stats.
    /// </summary>
    /// <param name="amount">Quantity of health points to recover.</param>
    public void Heal(int amount)
    {
        currentHealth += amount;

        // Clamp current health to ensure it doesn't exceed the maximum capacity
        if (currentHealth > maxHealth) 
        {
            currentHealth = maxHealth;
        }

        potionsCount++;

        PlayerAudio pAudio = GetComponent<PlayerAudio>();
        if (pAudio != null)
        {
            pAudio.PlayPotionSound();
        }

        Debug.Log("Restored " + amount + " HP. Current health: " + currentHealth);

        UpdateUI();
    }

    /// <summary>
    /// Updates the UI fill amount and color based on the current normalized health percentage.
    /// </summary>
    void UpdateUI()
    {
        if (healthBarFill != null)
        {
            float healthPercentage = visualHealth / maxHealth;
            
            healthBarFill.fillAmount = healthPercentage;
            
            if (healthGradient != null)
            {
                healthBarFill.color = healthGradient.Evaluate(healthPercentage);
            }
        }
    }

    /// <summary>
    /// Handles the player death sequence and triggers the Game Over state in the GameManager.
    /// </summary>
    void Die()
    {
        Debug.Log("Player died! Game Over.");
        if (GameManager.instance != null)
        {
            GameManager.instance.GameOver();
        }
    }
}