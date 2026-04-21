using UnityEngine;

/// <summary>
/// Orchestrates the first Enemy AI logic, managing states for patrolling, chasing,
/// and ranged combat. Synchronizes physics-based movement with frame-perfect 
/// Animation Events for damage delivery and invulnerability frames.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyAI : MonoBehaviour
{
    [Header("Core References")]
    [Tooltip("Reference to the player's transform for distance calculation and targeting.")]
    public Transform player;
    
    [Tooltip("Standard horizontal movement speed.")]
    public float moveSpeed = 3f;
    
    [Tooltip("Check this if the source sprite asset is oriented to the left by default.")]
    public bool invertFacingDirection = false;

    [Header("Patrol & Leash Configuration")]
    [Tooltip("The horizontal radius around the spawn point where the enemy is allowed to wander.")]
    public float maxPatrolDistance = 5f;
    private float startX;

    [Header("Detection Thresholds")]
    [Tooltip("Radius within which the enemy will notice the player and initiate a chase.")]
    public float chaseRange = 4f;        
    
    [Tooltip("Radius within which the enemy will halt movement to begin firing sequences.")]
    public float attackRange = 3f;       
    
    [Tooltip("Distance at which the enemy stops hunting and returns to its start position.")]
    public float loseAggroRange = 8f;    
    
    [Tooltip("The buffer zone maintained during reloads to prevent overlapping with the player.")]
    public float stopDistance = 1.5f;
    
    private bool isAggroed = false;      

    [Header("Combat Timing")]
    [Tooltip("Minimum cooldown duration (seconds) between attack bursts.")]
    public float minFireDelay = 0.7f;
    
    [Tooltip("Maximum cooldown duration (seconds) between attack bursts.")]
    public float maxFireDelay = 2.0f;
    
    private float nextFireTime = 0f;
    private bool canShoot = true;
    
    [HideInInspector]
    public bool isShooting = false;

    [Header("Wander Behavior")]
    [Tooltip("Frequency of direction changes during the patrol state.")]
    public float changeDirectionTime = 2f;
    private float randomMoveTimer;
    private Vector2 randomDirection;

    [Header("Combat Potency")]
    [Tooltip("Damage applied by the first frame-synced shot event.")]
    public int shot1Damage = 4;
    
    [Tooltip("Damage applied by the second frame-synced shot event.")]
    public int shot2Damage = 6;

    private Rigidbody2D rb;
    private Animator anim;
    private Vector3 startingScale;

    /// <summary>
    /// Initializes components and establishes the 'Leash' anchor point based on spawn coordinates.
    /// </summary>
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        startingScale = transform.localScale;
        startX = transform.position.x; 

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj) player = playerObj.transform;
    }

    /// <summary>
    /// Primary decision engine. Prioritizes state locks (shooting) before evaluating 
    /// distance-based transitions between Aggro and Patrol states.
    /// </summary>
    void Update()
    {
        if (player == null) return;

        // Physics Lock: Prevent sliding during active attack animations
        if (isShooting)
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            return;
        }

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);

        // Aggro State Management (Memory/Awareness logic)
        if (!isAggroed && distanceToPlayer <= chaseRange)
        {
            isAggroed = true; 
        }
        else if (isAggroed && distanceToPlayer > loseAggroRange)
        {
            isAggroed = false; 
        }

        // Behavior Execution
        if (isAggroed)
        {
            if (distanceToPlayer > attackRange)
            {
                LookAtPlayer();
                Chase();
            }
            else
            {
                LookAtPlayer();
                StopAndShoot();
            }
        }
        else
        {
            Patrol();
        }
    }

    /// <summary>
    /// Adjusts the transform scale to face the target. Includes a small deadzone 
    /// to mitigate rapid 'flip-flopping' when overlapping with the player.
    /// </summary>
    void LookAtPlayer()
    {
        float distanceX = player.position.x - transform.position.x;

        if (Mathf.Abs(distanceX) < 0.1f) 
        {
            return; 
        }

        float faceRightX = invertFacingDirection ? -Mathf.Abs(startingScale.x) : Mathf.Abs(startingScale.x);
        float faceLeftX = invertFacingDirection ? Mathf.Abs(startingScale.x) : -Mathf.Abs(startingScale.x);

        if (distanceX > 0)
            transform.localScale = new Vector3(faceRightX, startingScale.y, startingScale.z);
        else
            transform.localScale = new Vector3(faceLeftX, startingScale.y, startingScale.z);
    }

    /// <summary>
    /// Executes a wandering behavior. Uses Raycasting for obstacle avoidance 
    /// and a coordinate check to enforce the leash boundary.
    /// </summary>
    void Patrol()
    {
        // Obstacle Avoidance: Raycast in movement direction to detect walls
        Vector2 rayDir = randomDirection.x > 0 ? Vector2.right : Vector2.left;
        RaycastHit2D[] hits = Physics2D.RaycastAll(transform.position, rayDir, 1f);
        
        foreach (var hit in hits)
        {
            if (hit.collider.gameObject != gameObject && !hit.collider.CompareTag("Player") && !hit.collider.isTrigger)
            {
                randomDirection = new Vector2(-randomDirection.x, 0);
                randomMoveTimer = changeDirectionTime;
                break; 
            }
        }

        // Leash Enforcement: Force direction reversal if exceeding patrol limits
        if (transform.position.x > startX + maxPatrolDistance)
        {
            randomDirection = new Vector2(-1, 0);
            randomMoveTimer = changeDirectionTime;
        }
        else if (transform.position.x < startX - maxPatrolDistance)
        {
            randomDirection = new Vector2(1, 0);
            randomMoveTimer = changeDirectionTime;
        }
        else
        {
            // Standard direction switching based on timer
            randomMoveTimer -= Time.deltaTime;
            if (randomMoveTimer <= 0)
            {
                float randomX = Random.value > 0.5f ? 1f : -1f;
                randomDirection = new Vector2(randomX, 0).normalized;
                randomMoveTimer = changeDirectionTime;
            }
        }

        rb.linearVelocity = new Vector2(randomDirection.x * (moveSpeed / 2), rb.linearVelocity.y);

        float faceRightX = invertFacingDirection ? -Mathf.Abs(startingScale.x) : Mathf.Abs(startingScale.x);
        float faceLeftX = invertFacingDirection ? Mathf.Abs(startingScale.x) : -Mathf.Abs(startingScale.x);

        if (randomDirection.x > 0)
            transform.localScale = new Vector3(faceRightX, startingScale.y, startingScale.z);
        else if (randomDirection.x < 0)
            transform.localScale = new Vector3(faceLeftX, startingScale.y, startingScale.z);

        if (anim != null) anim.SetFloat("Speed", Mathf.Abs(randomDirection.x));
    }

    /// <summary>
    /// Translates the enemy towards the player's current X-coordinate.
    /// </summary>
    void Chase()
    {
        Vector2 targetPosition = new Vector2(player.position.x, transform.position.y);
        Vector2 moveDirection = (targetPosition - (Vector2)transform.position).normalized;

        rb.linearVelocity = new Vector2(moveDirection.x * moveSpeed, rb.linearVelocity.y);
        if (anim != null) anim.SetFloat("Speed", Mathf.Abs(moveDirection.x));
    }

    /// <summary>
    /// Manages the transition from chasing to attacking. If cooldowns are active, 
    /// maintains a follow-distance defined by stopDistance.
    /// </summary>
    void StopAndShoot()
    {
        if (canShoot && Time.time >= nextFireTime)
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            if (anim != null) anim.SetFloat("Speed", 0);
            
            StartShooting();
        }
        else
        {
            float distanceToPlayer = Vector2.Distance(transform.position, player.position);

            if (distanceToPlayer > stopDistance)
            {
                Chase(); 
            }
            else
            {
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                if (anim != null) anim.SetFloat("Speed", 0);
            }
        }
    }

    /// <summary>
    /// Initiates the combat sequence by triggering the animator and locking AI state.
    /// </summary>
    void StartShooting()
    {
        isShooting = true;
        canShoot = false;
        if (anim != null) anim.SetTrigger("Shoot");
    }

    // ========================================================================
    // ANIMATION EVENTS 
    // ========================================================================

    /// <summary>Damage trigger for the first visual shot frame.</summary>
    public void ExecuteShot1()
    {
        if (player != null && Vector2.Distance(transform.position, player.position) <= attackRange)
        {
            PlayerHealth pHealth = player.GetComponent<PlayerHealth>();
            if (pHealth != null) pHealth.TakeDamage(shot1Damage);
        }
    }

    /// <summary>Damage trigger for the second visual shot frame.</summary>
    public void ExecuteShot2()
    {
        if (player != null && Vector2.Distance(transform.position, player.position) <= attackRange)
        {
            PlayerHealth pHealth = player.GetComponent<PlayerHealth>();
            if (pHealth != null) pHealth.TakeDamage(shot2Damage);
        }
    }

    /// <summary>Enables invulnerability frames during the reload portion of the animation.</summary>
    public void StartReloadImmunity()
    {
        EnemyHealth eHealth = GetComponent<EnemyHealth>();
        if (eHealth != null) eHealth.isImmune = true;
    }

    /// <summary>Finalizes the combat sequence, releases movement locks, and rolls a new cooldown delay.</summary>
    public void FinishShooting()
    {
        isShooting = false;
        
        EnemyHealth eHealth = GetComponent<EnemyHealth>();
        if (eHealth != null) eHealth.isImmune = false;
        
        float randomDelay = Random.Range(minFireDelay, maxFireDelay);
        nextFireTime = Time.time + randomDelay;
        canShoot = true;
    }

    // ========================================================================

    /// <summary>
    /// Visualizes AI perception and leash boundaries within the Unity Editor.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, chaseRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, loseAggroRange);

        Gizmos.color = Color.green;
        float drawStartX = Application.isPlaying ? startX : transform.position.x;
        Vector3 leftLimit = new Vector3(drawStartX - maxPatrolDistance, transform.position.y, 0);
        Vector3 rightLimit = new Vector3(drawStartX + maxPatrolDistance, transform.position.y, 0);
        Gizmos.DrawLine(leftLimit, rightLimit);
        Gizmos.DrawSphere(leftLimit, 0.2f);
        Gizmos.DrawSphere(rightLimit, 0.2f);
        
        if (Application.isPlaying) {
            Gizmos.color = Color.white;
            Vector2 rayDir = randomDirection.x > 0 ? Vector2.right : Vector2.left;
            Gizmos.DrawRay(transform.position, rayDir * 1f);
        }
    }
}