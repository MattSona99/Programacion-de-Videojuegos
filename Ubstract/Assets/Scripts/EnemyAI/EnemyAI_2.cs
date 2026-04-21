using UnityEngine;
using System.Collections;
using UnityEngine.UI;

/// <summary>
/// Controls the AI for the second Boss type. 
/// Features a state machine managing spawn sequences, proximity-based melee combos, 
/// and a protected 'Crazy Mode' phase with shield mechanics and forced knockback.
/// </summary>
public class EnemyAI_2 : MonoBehaviour
{
    public enum EnemyState { Spawning, Walking, Crazy, Attacking }
    
    [Header("Current State")]
    public EnemyState currentState = EnemyState.Spawning;

    [Header("Movement Configuration")]
    public float moveSpeed = 2.5f;
    public float spawnDuration = 1.5f;
    public float stoppingDistance = 3f;

    [Header("Combat Potency")]
    public int normalDamage = 3;
    public int highDamage = 6;

    [Header("Attack Intervals")]
    [Tooltip("Minimum duration to wait before initiating the next combo sequence.")]
    public float minAttackDelay = 1.0f; 
    [Tooltip("Maximum duration to wait before initiating the next combo sequence.")]
    public float maxAttackDelay = 3.0f;
    
    private float nextAttackTime = 0f; 

    [HideInInspector] public int currentComboDamage = 0;
    [HideInInspector] public bool isCurrentHitUnblockable = false;

    [Header("Phase Transition: Crazy Mode")]
    [Tooltip("Number of parries required to break the Boss's protective shield.")]
    public int shieldUnits = 4;
    private int maxShieldUnits;
    public float knockbackForce = 5f;
    
    [Tooltip("Preparation time in seconds before the active strike occurs during the crazy loop.")]
    public float timeToHit = 1.20f; 
    [Tooltip("Recovery time in seconds after a crazy strike before the next iteration.")]
    public float timeAfterHit = 0.34f; 
    
    private int currentCrazyAttacks = 0;
    private int parriesReceived = 0;
    private bool isCurrentlyInCrazyLoop = false;

    private Animator anim;
    private Transform player;
    private EnemyHealth health;
    private Slider shieldBar;
    private Rigidbody2D rb;

    /// <summary>
    /// Initializes references and sets the boss to an initial immune spawning state.
    /// Dynamically locates the UI shield bar within the global HUD.
    /// </summary>
    void Start()
    {
        anim = GetComponent<Animator>();
        health = GetComponent<EnemyHealth>();
        rb = GetComponent<Rigidbody2D>();

        maxShieldUnits = shieldUnits;
        
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;

        if (health != null) health.isImmune = true;

        GameObject healthBG = GameObject.Find("EnemyHealthBG");
        if (healthBG != null)
        {
            Transform shieldTransform = healthBG.transform.Find("ShieldBar");
            if (shieldTransform != null)
            {
                shieldBar = shieldTransform.GetComponent<Slider>();
            }
        }

        StartCoroutine(SpawnRoutine());
    }

    /// <summary>
    /// Handles the timed entry sequence, transitioning from immunity to the active walking state.
    /// </summary>
    private IEnumerator SpawnRoutine()
    {
        yield return new WaitForSeconds(spawnDuration);

        if (health != null) health.isImmune = false;
        
        currentState = EnemyState.Walking;
        anim.SetBool("isWalking", true);
    }

    /// <summary>
    /// Executes the active state logic. Locks physics during attack frames and 
    /// continuously monitors combo validity.
    /// </summary>
    void Update()
    {
        switch (currentState)
        {
            case EnemyState.Walking:
                MoveTowardsPlayer();
                break;
            case EnemyState.Attacking:
                // Lock horizontal velocity during combo execution to ensure rooted strikes
                if (rb != null) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
                CheckComboValidity();
                break;
        }
    }

    /// <summary>
    /// Manages locomotion and orientation toward the player. Triggers the attack 
    /// state if within stoppingDistance and the random cooldown has elapsed.
    /// </summary>
    void MoveTowardsPlayer()
    {
        if (player == null || rb == null) return;

        float distanceToPlayer = Mathf.Abs(player.position.x - transform.position.x);

        // Adjust facing direction based on player relative position
        if (distanceToPlayer > 0.1f)
        {
            if (player.position.x > transform.position.x)
                transform.localScale = new Vector3(-1, 1, 1); 
            else
                transform.localScale = new Vector3(1, 1, 1);  
        }

        if (distanceToPlayer > stoppingDistance)
        {
            float moveDirection = player.position.x > transform.position.x ? 1f : -1f;
            rb.linearVelocity = new Vector2(moveDirection * moveSpeed, rb.linearVelocity.y);
            anim.SetBool("isWalking", true); 
        }
        else
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

            if (Time.time >= nextAttackTime)
            {
                StartBaseAttack(); 
            }
            else
            {
                anim.SetBool("isWalking", false); 
            }
        }
    }

    /// <summary>
    /// Transitions into the combat state and triggers the animator's combo sequence.
    /// </summary>
    private void StartBaseAttack()
    {
        currentState = EnemyState.Attacking;
        anim.SetBool("isWalking", false);
        anim.SetTrigger("Attack"); 
    }

    /// <summary>
    /// Animation Event: Finalizes the combat state and calculates the next random attack window.
    /// </summary>
    public void AnimEvent_FinishAttack()
    {
        if (currentState == EnemyState.Crazy) return; 

        currentState = EnemyState.Walking;
        anim.SetBool("isWalking", true);

        float randomDelay = Random.Range(minAttackDelay, maxAttackDelay);
        nextAttackTime = Time.time + randomDelay;

        AnimEvent_ResetHit(); 
    }

    /// <summary>
    /// Initiates Phase Transition logic. Enforces immunity, resets parry counters, 
    /// and triggers a cinematic knockback on the player before starting the phase loop.
    /// </summary>
    public void StartCrazyMode()
    {
        if (isCurrentlyInCrazyLoop) return; 
        
        currentState = EnemyState.Crazy;
        isCurrentlyInCrazyLoop = true;
        
        health.isImmune = true; 
        parriesReceived = 0;
        currentCrazyAttacks = shieldUnits; 

        if (player != null)
        {
            // Face the player before applying the explosive knockback force
            if (player.position.x > transform.position.x)
            {
                transform.localScale = new Vector3(-1, 1, 1); 
            }
            else
            {
                transform.localScale = new Vector3(1, 1, 1);  
            }

            StartCoroutine(ApplyKnockbackRoutine());
        }

        if (shieldBar != null) 
        {
            shieldBar.gameObject.SetActive(true);
            shieldBar.maxValue = maxShieldUnits;
            shieldBar.value = shieldUnits;
        }

        anim.SetBool("isCrazy", true);
        anim.SetBool("isWalking", false);

        Enemy2Audio eAudio = GetComponent<Enemy2Audio>();
        if (eAudio != null) eAudio.PlayCrazyActivation();

        StartCoroutine(CrazySequenceRoutine());
    }

    /// <summary>
    /// Drives the iterative logic of the Crazy Mode phase.
    /// </summary>
    private IEnumerator CrazySequenceRoutine()
    {
        for (int i = 0; i < currentCrazyAttacks; i++) 
        {
            yield return new WaitForSeconds(timeToHit);
            Debug.Log("Executing Crazy Strike sequence: " + (i + 1));
            yield return new WaitForSeconds(timeAfterHit);
        }

        EndCrazyMode();
    }

    /// <summary>
    /// Disables player input and applies a deterministic velocity impulse to launch 
    /// the player away during phase transitions.
    /// </summary>
    private IEnumerator ApplyKnockbackRoutine()
    {
        Rigidbody2D playerRb = player.GetComponent<Rigidbody2D>();
        MonoBehaviour moveScript = (MonoBehaviour)player.GetComponent("PlayerMovements");
        MonoBehaviour combatScript = (MonoBehaviour)player.GetComponent("PlayerCombat");

        if (playerRb != null)
        {
            // Prevent player interference during the knockback trajectory
            if (moveScript != null) moveScript.enabled = false;
            if (combatScript != null) combatScript.enabled = false;

            Vector2 knockbackDir = (player.position - transform.position).normalized;
            
            // Normalize direction and apply upward bias to reduce floor friction during flight
            knockbackDir.y = 1f; 
            knockbackDir.x = knockbackDir.x > 0 ? 1f : -1f; 

            // Hard velocity override ensures consistent distance regardless of prior momentum
            playerRb.linearVelocity = knockbackDir * knockbackForce; 

            yield return new WaitForSeconds(0.5f);

            // Halt the player movement before returning control
            playerRb.linearVelocity = Vector2.zero;

            if (moveScript != null) moveScript.enabled = true;
            if (combatScript != null) combatScript.enabled = true;
        }
    }

    /// <summary>
    /// Public interface called by the player's parry system. 
    /// Reduces the Boss's specialized phase shield.
    /// </summary>
    public void OnParryReceived()
    {
        if (currentState == EnemyState.Crazy) {
            parriesReceived++;
            shieldUnits--; 
            Enemy2Audio eAudio = GetComponent<Enemy2Audio>();
            if (eAudio != null) eAudio.PlayShieldBroken();
            if (shieldUnits < 0) shieldUnits = 0; 

            if (shieldBar != null) {
                shieldBar.value = shieldUnits;
            }

            Debug.Log("Parry successful! Remaining shield units: " + shieldUnits);
        }
    }

    /// <summary>
    /// Evaluates the outcome of the Crazy Mode. If the shield is broken, forces 
    /// a permanent phase pass; otherwise, restores Boss health based on failed parries.
    /// </summary>
    private void EndCrazyMode()
    {
        isCurrentlyInCrazyLoop = false;
        
        anim.SetBool("isCrazy", false);
        anim.SetBool("isWalking", true);

        if (shieldBar != null) {
            shieldBar.gameObject.SetActive(false);
        }

        int failedParries = currentCrazyAttacks - parriesReceived;
        
        if (shieldUnits <= 0) {
            Debug.Log("Shield shattered! Advancing to the next combat phase.");
            health.isImmune = false;
            health.ForcePassThreshold(); 
            currentState = EnemyState.Walking;
            
            // Re-initialize shield configuration for potential future usage
            ResetShield(); 

        } else {
            // Heal the Boss based on how many parries the player missed (25% per failure)
            float regenPercent = failedParries * 0.25f;
            health.Regenerate(regenPercent);
            
            Debug.Log("Phase survived by Boss. Regeneration applied: " + (regenPercent * 100) + "%");
            health.isImmune = false;
            currentState = EnemyState.Walking;
        }
    }

    /// <summary>
    /// Restores the shield value to its original capacity and hides the UI element.
    /// </summary>
    public void ResetShield()
    {
        shieldUnits = maxShieldUnits; 
    
        if (shieldBar != null)
        {
            shieldBar.maxValue = maxShieldUnits;
            shieldBar.value = maxShieldUnits;
            shieldBar.gameObject.SetActive(false); 
        }
    
        Debug.Log("Shield values reset for subsequent phases.");
    }

    // ========================================================================
    // ANIMATION EVENTS (Combat Synchronization)
    // ========================================================================

    /// <summary>Sets standard damage parameters for initial combo strikes.</summary>
    public void AnimEvent_SetNormalHit() { 
        currentComboDamage = normalDamage; 
        isCurrentHitUnblockable = false; 
    }

    /// <summary>Sets slightly increased damage parameters for mid-combo strikes.</summary>
    public void AnimEvent_SetBuffedHit() { 
        currentComboDamage = normalDamage + 1; 
        isCurrentHitUnblockable = false; 
    }

    /// <summary>Sets high damage parameters for heavy combo strikes.</summary>
    public void AnimEvent_SetHeavyHit() { 
        currentComboDamage = highDamage; 
        isCurrentHitUnblockable = false; 
    }

    /// <summary>Handles the unblockable earthquake strike. Triggers the player's earthquake handler if in range.</summary>
    public void AnimEvent_SetGroundHit() { 
        currentComboDamage = highDamage; 
        isCurrentHitUnblockable = true; 

        if (player != null)
        {
            float distance = Vector2.Distance(transform.position, player.position);

            if (distance < 5.0f)
            {
                player.SendMessage("TakeEarthquakeDamage", currentComboDamage, SendMessageOptions.DontRequireReceiver);
            }
        }
    }

    /// <summary>Clears active damage parameters at the end of combat sequences.</summary>
    public void AnimEvent_ResetHit() { 
        currentComboDamage = 0; 
        isCurrentHitUnblockable = false; 
    }

    /// <summary>
    /// Validates the spatial relationship between Boss and Player. 
    /// Cancels the active combo if the player moves out of range or gets behind the Boss.
    /// </summary>
    private void CheckComboValidity()
    {
        if (player == null) return;

        float distance = Mathf.Abs(player.position.x - transform.position.x);
        bool isPlayerToTheRight = player.position.x > transform.position.x;
        bool isFacingRight = transform.localScale.x < 0; 

        // Logical check for player being behind the Boss's active strike zone
        bool isPlayerBehind = (isPlayerToTheRight && !isFacingRight) || (!isPlayerToTheRight && isFacingRight);

        if (distance > 5.0f || isPlayerBehind)
        {
            CancelCombo();
        }
    }

    /// <summary>
    /// Forcefully interrupts the combat sequence, resets hit logic, and plays 
    /// the locomotion animation to prevent combat lock.
    /// </summary>
    private void CancelCombo()
    {
        AnimEvent_ResetHit();

        currentState = EnemyState.Walking;
        anim.SetBool("isWalking", true);

        // Immediate override of the animator's state machine to the walking clip
        anim.Play("Enemy2Walking"); 

        float randomDelay = Random.Range(minAttackDelay, maxAttackDelay);
        nextAttackTime = Time.time + 0.5f;
        
        Debug.Log("Combo sequence aborted: Target escaped the strike zone.");
    }
}