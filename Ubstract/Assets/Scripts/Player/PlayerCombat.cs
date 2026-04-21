using UnityEngine;

/// <summary>
/// Handles player combat mechanics, attacking, defending, 
/// and instantly reflecting damage on a Perfect Parry.
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    private Animator anim;

    [Header("Damage Configuration")]
    [Tooltip("Damage dealt by the first punch in the combo sequence.")]
    public int punch1Damage = 2;
    [Tooltip("Damage dealt by the second punch in the combo sequence.")]
    public int punch2Damage = 3;

    [Header("Hitbox Detection")]
    [Tooltip("The origin point for the melee attack overlap circle.")]
    public Transform attackPoint;
    [Tooltip("The radius of the melee attack detection.")]
    public float attackRange = 0.4f;
    [Tooltip("The layer(s) identifying valid enemy targets.")]
    public LayerMask enemyLayers;

    [Header("Loot Drop Settings")]
    [Tooltip("The prefab to spawn when a health drop is triggered.")]
    public GameObject healthDropPrefab;
    [Tooltip("Horizontal force applied to the dropped item.")]
    public float dropForceX = 4f;
    [Tooltip("Vertical force applied to the dropped item.")]
    public float dropForceY = 6f;
    [Tooltip("The percentage chance (0-100) for a health item to drop upon parrying.")]
    public float dropChance = 30f;

    [HideInInspector] public bool isAttacking = false;
    [HideInInspector] public bool isDefending = false;

    /// <summary>
    /// Initializes internal component references.
    /// </summary>
    void Start()
    {
        anim = GetComponent<Animator>();
    }

    /// <summary>
    /// Monitors player input and updates Animator parameters.
    /// Defending takes priority over attacking to prevent animation overlapping.
    /// </summary>
    void Update()
    {
        isDefending = Input.GetMouseButton(1);
        
        anim.SetBool("isDefending", isDefending);
        anim.SetBool("isAttacking", isAttacking);

        if (isDefending)
        {
            isAttacking = false;
            anim.SetBool("isAttacking", false);
            return;
        }

        if (Input.GetMouseButtonDown(0) && !isAttacking)
        {
            Attack();
        }
    }

    /// <summary>
    /// Sets the attack state and triggers the corresponding animation.
    /// </summary>
    void Attack()
    {
        isAttacking = true;
        anim.SetTrigger("Attack"); 
    }

    // ========================================================================
    // ANIMATION EVENTS 
    // ========================================================================

    /// <summary>
    /// Triggered via Animation Event to process damage for the first punch.
    /// </summary>
    public void ExecutePunch1() 
    {
        DealDamageToEnemies(punch1Damage);
        PlayerAudio pAudio = GetComponent<PlayerAudio>();
        if (pAudio != null)
        {
            pAudio.PlaySound(pAudio.punch1Sound);
        }
    }

    /// <summary>
    /// Triggered via Animation Event to process damage for the second punch.
    /// </summary>
    public void ExecutePunch2() 
    {
        DealDamageToEnemies(punch2Damage);
        PlayerAudio pAudio = GetComponent<PlayerAudio>();
        if (pAudio != null)
        {
            pAudio.PlaySound(pAudio.punch2Sound);
        }
    }

    /// <summary>
    /// Triggered via Animation Event at the end of the combat clip to release the attack state.
    /// </summary>
    public void FinishAttack() 
    {
        isAttacking = false;
    }

    // ========================================================================

    /// <summary>
    /// Detects enemies within the attack radius using a circular overlap check 
    /// and applies the damage value through the EnemyHealth component.
    /// </summary>
    void DealDamageToEnemies(int baseDamage)
    {
        if (attackPoint == null) return;

        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, enemyLayers);

        foreach (Collider2D enemy in hitEnemies)
        {
            EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(baseDamage);
            }
        }
    }

    /// <summary>
    /// Processes the damage reflection mechanic. Invoked by PlayerHealth during a 
    /// perfect parry to instantly damage and stagger enemies within a wide area.
    /// </summary>
    /// <param name="reflectedDamage">The amount of damage to return to the attacker.</param>
    public void ReflectDamage(int reflectedDamage)
    {
        // Broad search to find any active enemies in the arena context
        Collider2D[] enemiesInArena = Physics2D.OverlapCircleAll(transform.position, 15f, enemyLayers);

        foreach (Collider2D enemy in enemiesInArena)
        {
            EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(reflectedDamage);
            }

            PlayerAudio pAudio = GetComponent<PlayerAudio>();
            if (pAudio != null)
            {
                pAudio.PlaySound(pAudio.perfectParrySound);
            }

            // Notifies Enemy AI to handle parry-specific logic (e.g., stagger states)
            EnemyAI_2 aiScript = enemy.GetComponent<EnemyAI_2>();
            if (aiScript != null)
            {
                aiScript.OnParryReceived();
            }

            SpawnHealthDrop();
        }
    }

    /// <summary>
    /// Calculates RNG for health drops and applies physics impulses to the 
    /// instantiated prefab to create a dynamic loot effect.
    /// </summary>
    private void SpawnHealthDrop()
    {
        float randomRoll = Random.Range(0f, 100f);

        if (randomRoll > dropChance) 
        {
            Debug.Log($"No drop triggered. Roll: {randomRoll:F1}%. Threshold: {dropChance}%");
            return; 
        }

        if (healthDropPrefab == null) return;

        // Positioning logic: Spawns the item slightly above the player's head
        Vector3 spawnPos = transform.position + new Vector3(0, 1.5f, 0);
        
        GameObject drop = Instantiate(healthDropPrefab, spawnPos, Quaternion.identity);

        // Physics logic: Applies a random horizontal arc using ForceMode2D.Impulse
        Rigidbody2D rb = drop.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            float randomDir = Random.value > 0.5f ? 1f : -1f;
            
            Vector2 force = new Vector2(dropForceX * randomDir, dropForceY);
            rb.AddForce(force, ForceMode2D.Impulse);
        }
    }

    /// <summary>
    /// Visualization tool to debug melee hitboxes and the parry reflection radius.
    /// </summary>
    void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, attackRange);
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 15f);
    }
}