using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the enemy's health pool, UI updates, and incoming damage.
/// Includes immunity states and universal phase transitions for different Bosses.
/// </summary>
public class EnemyHealth : MonoBehaviour
{
    [Header("Vitality Stats")]
    [Tooltip("The maximum amount of health the enemy can have.")]
    public int maxHealth = 200;

    [Tooltip("The amount of health each visual health bar segment represents.")]
    public int healthPerBar = 100;

    [Header("Phase Transition Configuration")]
    [Tooltip("Health interval required to trigger a phase transition. Can be overridden by AI scripts.")]
    public int phaseTriggerStep = 100;
    
    private int currentHealth;
    
    private EnemyAI_2 aiScript2;
    private EnemyAI_3 aiScript3;
    
    private MatchTracker matchTracker;

    [Header("Health Bar UI")]
    public Image healthBarFill;
    public float lerpSpeed = 5f;
    private float visualHealth;

    public Gradient baseBarGradient;
    public Gradient extraBarGradient;

    [HideInInspector]
    public bool isImmune = false;

    [Header("Visual Feedback")]
    public FloatingText floatingTextPrefab;

    [Header("Dynamic Frame Settings")]
    public Sprite enemyFrame;
    public Vector2 frameOffset;

    [Header("Enemy Identity")]
    public string enemyName;
    public TMPro.TextMeshProUGUI enemyNameText;

    /// <summary>
    /// Initializes health values and caches references to the MatchTracker and active AI components.
    /// Dynamically links and configures the Boss UI elements.
    /// </summary>
    void Start()
    {
        currentHealth = maxHealth;
        visualHealth = maxHealth;

        matchTracker = FindFirstObjectByType<MatchTracker>();

        aiScript2 = GetComponent<EnemyAI_2>();
        aiScript3 = GetComponent<EnemyAI_3>();

        if (healthBarFill == null)
        {
            GameObject uiBar = GameObject.Find("EnemyHealthFill"); 
            if (uiBar != null) healthBarFill = uiBar.GetComponent<Image>();
            else Debug.LogWarning("Enemy health bar fill not found in Canvas!");
        }

        GameObject frameObj = GameObject.Find("EnemyFrameHB");
        if (frameObj != null && enemyFrame != null)
        {
            Image frameImage = frameObj.GetComponent<Image>();
            frameImage.sprite = enemyFrame;
            frameImage.SetNativeSize();

            RectTransform rect = frameObj.GetComponent<RectTransform>();
            rect.anchoredPosition = frameOffset;
        }

        UpdateUI();

        if (enemyNameText == null) 
        {
            GameObject nameObj = GameObject.Find("EnemyName");
            if (nameObj != null)
            {
                enemyNameText = nameObj.GetComponent<TMPro.TextMeshProUGUI>();
                if (enemyNameText != null) enemyNameText.text = enemyName;
            }
        }
        else
        {
             enemyNameText.text = enemyName;
        }
    }

    /// <summary>
    /// Processes incoming damage. Uses modular arithmetic to dynamically calculate thresholds 
    /// based on 'phaseTriggerStep' to trigger transitions instead of instant death.
    /// </summary>
    /// <param name="damageAmount">The amount of health to subtract.</param>
    public void TakeDamage(int damageAmount)
    {
        if (isImmune)
        {
            if (floatingTextPrefab) 
            {
                FloatingText text = Instantiate(floatingTextPrefab, transform.position, Quaternion.identity);
                text.Setup("IMMUNE!", Color.white);
            }
            return;
        }

        if (matchTracker != null) matchTracker.AddDamageDealt(damageAmount);

        int oldHealth = currentHealth;
        currentHealth -= damageAmount;

        // Dynamic threshold calculation (e.g., if step is 50 and oldHealth is 140, targetThreshold is 100)
        int targetThreshold = ((oldHealth - 1) / phaseTriggerStep) * phaseTriggerStep;
        bool thresholdCrossed = currentHealth <= targetThreshold;

        if (thresholdCrossed && currentHealth < oldHealth)
        {
            // If the entity is a Boss with a defined phase transition
            if (aiScript2 != null || aiScript3 != null)
            {
                // Lock health just above the threshold to prevent skipping phases
                currentHealth = (targetThreshold > 0) ? (targetThreshold + 1) : 1;
                UpdateUI();

                // Invoke the specific phase logic on the active AI script
                if (aiScript2 != null)
                {
                    aiScript2.StartCrazyMode();
                    return;
                }
                else if (aiScript3 != null)
                {
                    aiScript3.StartVerticalPhase();
                    return;
                }
            }
        }

        UpdateUI();

        if (floatingTextPrefab) 
        {
            FloatingText text = Instantiate(floatingTextPrefab, transform.position, Quaternion.identity);
            text.Setup(damageAmount.ToString(), Color.red);
        }
        
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// Restores health points based on a percentage of the health bar capacity.
    /// </summary>
    public void Regenerate(float percent) {
        int amount = Mathf.RoundToInt(healthPerBar * percent);
        currentHealth = Mathf.Clamp(currentHealth + amount, 0, maxHealth);
        UpdateUI();
    }

    /// <summary>
    /// Logic gate invoked by the AI once transition sequences are finalized. 
    /// Forcefully pushes health below the segment threshold to resume normal gameplay.
    /// </summary>
    public void ForcePassThreshold()
    {
        currentHealth -= 5;
        UpdateUI();

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// Updates the visual health bar using linear interpolation for a smooth decay effect.
    /// </summary>
    void Update()
    {
        if (Mathf.Abs(visualHealth - currentHealth) > 0.01f)
        {
            visualHealth = Mathf.Lerp(visualHealth, currentHealth, Time.deltaTime * lerpSpeed);
            UpdateUI(); 
        }
    }

    /// <summary>
    /// Handles the visual representation of multi-layered health bars.
    /// </summary>
    void UpdateUI()
    {
        if (healthBarFill != null)
        {
            if (visualHealth > healthPerBar)
            {
                float extraHealth = visualHealth - healthPerBar;
                float extraPercentage = extraHealth / healthPerBar;
                
                healthBarFill.fillAmount = extraPercentage;
                healthBarFill.color = extraBarGradient.Evaluate(extraPercentage);
            }
            else
            {
                float healthPercentage = visualHealth / healthPerBar;
                
                healthBarFill.fillAmount = healthPercentage;
                healthBarFill.color = baseBarGradient.Evaluate(healthPercentage);
            }
        }
    }

    /// <summary>
    /// Disposes of the enemy object, updates global match statistics.
    /// </summary>
    void Die()
    {
        Debug.Log("Enemy died!");

        if (matchTracker != null) matchTracker.AddEnemyDefeated();
        if (GameManager.instance != null) GameManager.instance.OnEnemyDefeated();
        
        Destroy(gameObject);
    }
}