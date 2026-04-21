using UnityEngine;

/// <summary>
/// Controls the advanced AI for the third Boss type. 
/// Features complex parabolic movement patterns, wall-jump mechanics, 
/// and a vertical phase transition where the boss moves to the ceiling.
/// </summary>
public class EnemyAI_3 : MonoBehaviour
{
    public enum EnemyState { Walking, Idle, Attacking, PhaseTransition }
    
    [Header("Current State")]
    public EnemyState currentState = EnemyState.Walking;

    [Header("Movement Configuration")]
    public float moveSpeed = 3.0f; 
    public float stoppingDistance = 3.5f;

    [Header("Attack Intervals")]
    public float minAttackDelay = 2.0f; 
    public float maxAttackDelay = 5.0f;
    private float nextAttackTime = 0f;

    [Header("Attack 1: Short Jump Strike")]
    [Tooltip("Peak height of the jump during the tracking strike.")]
    public float jumpHeight = 2.5f; 
    [Tooltip("Total time in seconds to complete the leap from start to impact.")]
    public float jumpDuration = 0.8f; 

    [Header("Attack 2: Wall-Bounce Strike")]
    [Tooltip("The horizontal distance traveled backwards to hit the arena wall boundary.")]
    public float wallBackDistance = 5.0f; 
    [Tooltip("The vertical height reached on the wall before launching the strike.")]
    public float wallHeight = 3.0f; 
    [Tooltip("Duration of the initial jump to the wall.")]
    public float wallJumpDuration = 0.4f; 
    [Tooltip("Duration of the high-speed descent strike from the wall to the player.")]
    public float wallStrikeDuration = 0.6f; 
    [Tooltip("The arc peak during the high-speed descent.")]
    public float wallStrikeArcHeight = 2.0f; 

    [Header("Phase Transition: Ceiling Phase")]
    [Tooltip("Maximum vertical height reached during the ceiling phase transition.")]
    public float ceilingHeight = 2.0f; 
    [Tooltip("Time taken to reach the ceiling height.")]
    public float ascendDuration = 1.0f;
    [Tooltip("Time taken to return to the ground after the phase ends.")]
    public float descendDuration = 0.5f;

    private bool isAscending = false;
    private bool isDescending = false;
    private EnemyHealth healthScript;

    private Vector3 jumpStartPos;
    private Vector3 jumpTargetPos;
    private float jumpTimer;
    private float groundY; 

    private bool isJumping = false;
    
    private bool isJumpingToWall = false;
    private bool isHangingOnWall = false;
    private bool isWallStriking = false;

    private Animator anim;
    private Transform player;
    private Rigidbody2D rb;

    /// <summary>
    /// Initializes references and sets the first random attack cooldown.
    /// </summary>
    void Start()
    {
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
        healthScript = GetComponent<EnemyHealth>();
        nextAttackTime = Time.time + Random.Range(minAttackDelay, maxAttackDelay);
    }

    /// <summary>
    /// Main logic update. Prioritizes mathematical trajectory calculations for jumps 
    /// over standard locomotion to ensure precise movement during attack states.
    /// </summary>
    void Update()
    {
        if (player == null || rb == null) return;

        // --- Transition Movement Math ---
        if (isAscending) { HandleAscendMath(); return; }
        if (isDescending) { HandleDescendMath(); return; }

        if (currentState == EnemyState.PhaseTransition)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // Lock physics engine during custom lerp-based attacks to prevent interference
        if (currentState == EnemyState.Attacking)
        {
            rb.linearVelocity = Vector2.zero; 
        }

        // --- Attack 1 Logic ---
        if (isJumping) { HandleJumpMath(); return; }

        // --- Attack 2 Logic ---
        if (isJumpingToWall) { HandleWallJumpMath(); return; }
        
        if (isHangingOnWall) 
        { 
            LookAtPlayer(); 
            return; 
        }
        
        if (isWallStriking) { HandleWallStrikeMath(); return; }

        if (currentState == EnemyState.Attacking) return;

        // --- Locomotion Logic ---
        float distanceToPlayer = Mathf.Abs(player.position.x - transform.position.x);
        LookAtPlayer();

        if (distanceToPlayer > stoppingDistance) Move();
        else StopAndCheckAttack();
    }

    // ==========================================
    // TRAJECTORY MATHEMATICS
    // ==========================================

    /// <summary>
    /// Calculates parabolic trajectory for Attack 1 using Lerp and a Sine wave.
    /// </summary>
    private void HandleJumpMath()
    {
        jumpTimer += Time.deltaTime;
        float t = jumpTimer / jumpDuration;
        float xPos = Mathf.Lerp(jumpStartPos.x, jumpTargetPos.x, t);
        float yPos = Mathf.Lerp(jumpStartPos.y, jumpTargetPos.y, t) + (jumpHeight * Mathf.Sin(t * Mathf.PI));
        transform.position = new Vector3(xPos, yPos, jumpStartPos.z);
        if (t >= 1f) { isJumping = false; transform.position = jumpTargetPos; }
    }

    /// <summary>
    /// Calculates the initial leap backwards to hit the wall anchor point.
    /// </summary>
    private void HandleWallJumpMath()
    {
        jumpTimer += Time.deltaTime;
        float t = jumpTimer / wallJumpDuration;
        float xPos = Mathf.Lerp(jumpStartPos.x, jumpTargetPos.x, t);
        float yPos = Mathf.Lerp(jumpStartPos.y, jumpTargetPos.y, t) + (1.5f * Mathf.Sin(t * Mathf.PI)); 
        transform.position = new Vector3(xPos, yPos, jumpStartPos.z);
        
        if (t >= 1f) 
        { 
            isJumpingToWall = false; 
            transform.position = jumpTargetPos; 
            isHangingOnWall = true; 
        }
    }

    /// <summary>
    /// Calculates the aggressive descent from the wall to the player's ground position.
    /// </summary>
    private void HandleWallStrikeMath()
    {
        jumpTimer += Time.deltaTime;
        float t = jumpTimer / wallStrikeDuration;
        float xPos = Mathf.Lerp(jumpStartPos.x, jumpTargetPos.x, t);
        float baseLerpY = Mathf.Lerp(jumpStartPos.y, jumpTargetPos.y, t);
        float yPos = baseLerpY + (wallStrikeArcHeight * Mathf.Sin(t * Mathf.PI));
        transform.position = new Vector3(xPos, yPos, jumpStartPos.z);
        
        if (t >= 1f) 
        { 
            isWallStriking = false; 
            transform.position = jumpTargetPos; 
        }
    }

    // ==========================================
    // CORE BEHAVIORS
    // ==========================================

    /// <summary>
    /// Flips the local scale to ensure the boss is always facing the target.
    /// </summary>
    void LookAtPlayer()
    {
        if (player.position.x > transform.position.x) transform.localScale = new Vector3(-1, 1, 1);
        else transform.localScale = new Vector3(1, 1, 1);
    }

    /// <summary>
    /// Basic walking behavior towards the player.
    /// </summary>
    void Move()
    {
        currentState = EnemyState.Walking;
        float moveDirection = player.position.x > transform.position.x ? 1f : -1f;
        rb.linearVelocity = new Vector2(moveDirection * moveSpeed, rb.linearVelocity.y);
        anim.SetBool("isWalking", true);
    }

    /// <summary>
    /// Stops movement and evaluates the attack timer.
    /// </summary>
    void StopAndCheckAttack()
    {
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        anim.SetBool("isWalking", false);
        currentState = EnemyState.Idle;

        if (Time.time >= nextAttackTime) ExecuteAttack();
    }

    /// <summary>
    /// Randomly selects an attack sequence and triggers the associated animator parameter.
    /// </summary>
    void ExecuteAttack()
    {
        currentState = EnemyState.Attacking;
        groundY = transform.position.y; 

        if (Random.value > 0.5f) anim.SetTrigger("Attack1");
        else anim.SetTrigger("Attack2");
    }

    // ========================================================
    // --- ANIMATION EVENTS: ATTACK 1 ---
    // ========================================================

    /// <summary>Triggered at the start of the jump to lock in the player's current X position.</summary>
    public void AnimEvent_StartTracking() { 
        if (player == null) return;
        jumpStartPos = transform.position;
        jumpTargetPos = new Vector3(player.position.x, groundY, jumpStartPos.z); 
        jumpTimer = 0f; isJumping = true; 
    }

    /// <summary>Checks distance to player to apply melee damage.</summary>
    public void AnimEvent_DealDamage() { 
        if (player == null) return;
        if (Vector2.Distance(transform.position, player.position) <= 1.5f) 
        {
            PlayerHealth ph = player.GetComponent<PlayerHealth>();
            if (ph != null) ph.TakeDamage(10); 
        }
    }

    // ========================================================
    // --- ANIMATION EVENTS: ATTACK 2 (WALL STRIKE) ---
    // ========================================================

    /// <summary>Initiates the backwards jump to reach the wall boundary.</summary>
    public void AnimEvent_JumpToWall()
    {
        if (player == null) return;
        jumpStartPos = transform.position;
        
        float backwardDir = player.position.x > transform.position.x ? -1f : 1f;
        jumpTargetPos = new Vector3(transform.position.x + (backwardDir * wallBackDistance), groundY + wallHeight, jumpStartPos.z);
        
        jumpTimer = 0f;
        isJumpingToWall = true; 
    }

    /// <summary>Transitions from the wall-hang to the aggressive strike toward the player.</summary>
    public void AnimEvent_WallStrike()
    {
        if (player == null) return;
        isHangingOnWall = false; 
        
        jumpStartPos = transform.position; 
        jumpTargetPos = new Vector3(player.position.x, groundY, jumpStartPos.z); 
        
        jumpTimer = 0f;
        isWallStriking = true; 
    }

    /// <summary>Applies high damage in a larger radius upon landing the wall strike.</summary>
    public void AnimEvent_SlamDamage()
    {
        if (player == null) return;
        if (Vector2.Distance(transform.position, player.position) <= 10f) 
        {
            PlayerHealth ph = player.GetComponent<PlayerHealth>();
            if (ph != null) ph.TakeDamage(15); 
        }
    }

    // ========================================================
    // --- SHARED COMPLETION LOGIC ---
    // ========================================================

    /// <summary>Clears all jump flags and returns the boss to a walking state.</summary>
    public void AnimEvent_FinishAttack()
    {
        isJumping = false; 
        isJumpingToWall = false;
        isHangingOnWall = false;
        isWallStriking = false;
        
        currentState = EnemyState.Walking; 
        if (anim != null) anim.SetBool("isWalking", true);

        float randomDelay = Random.Range(minAttackDelay, maxAttackDelay);
        nextAttackTime = Time.time + randomDelay;
    }

    /// <summary>Calculates the linear vertical movement toward the ceiling.</summary>
    private void HandleAscendMath()
    {
        jumpTimer += Time.deltaTime;
        float t = jumpTimer / ascendDuration;
        float yPos = Mathf.Lerp(jumpStartPos.y, jumpTargetPos.y, t);
        transform.position = new Vector3(transform.position.x, yPos, jumpStartPos.z);
        
        if (t >= 1f) 
        { 
            isAscending = false; 
            transform.position = jumpTargetPos; 
        }
    }

    /// <summary>Calculates the linear vertical descent back to the floor.</summary>
    private void HandleDescendMath()
    {
        jumpTimer += Time.deltaTime;
        float t = jumpTimer / descendDuration;
        float yPos = Mathf.Lerp(jumpStartPos.y, jumpTargetPos.y, t);
        transform.position = new Vector3(transform.position.x, yPos, jumpStartPos.z);
        
        if (t >= 1f) 
        { 
            isDescending = false; 
            transform.position = jumpTargetPos; 
        }
    }

    /// <summary>Initiates the cinematic vertical phase. Grants immunity while moving to the ceiling.</summary>
    public void StartVerticalPhase()
    {
        isJumping = false;
        isJumpingToWall = false;
        isHangingOnWall = false;
        isWallStriking = false;

        currentState = EnemyState.PhaseTransition;
        rb.linearVelocity = Vector2.zero;

        if (healthScript != null) healthScript.isImmune = true;

        groundY = transform.position.y;

        anim.SetTrigger("VerticalPhase"); 
    }

    /// <summary>Triggered by animator when the boss begins ascending to the ceiling.</summary>
    public void AnimEvent_StartAscend()
    {
        jumpStartPos = transform.position;
        jumpTargetPos = new Vector3(transform.position.x, groundY + ceilingHeight, jumpStartPos.z);
        jumpTimer = 0f;
        isAscending = true;
    }

    /// <summary>Triggered by animator when the boss releases the ceiling to drop back down.</summary>
    public void AnimEvent_StartDescend()
    {
        jumpStartPos = transform.position;
        jumpTargetPos = new Vector3(transform.position.x, groundY, jumpStartPos.z);
        jumpTimer = 0f;
        isDescending = true;
    }

    /// <summary>Finalizes the vertical phase, restores vulnerability, and forces a health segment threshold check.</summary>
    public void AnimEvent_FinishPhase()
    {
        isAscending = false;
        isDescending = false;
        
        currentState = EnemyState.Walking; 
        if (anim != null) anim.SetBool("isWalking", true);

        if (healthScript != null)
        {
            healthScript.isImmune = false;
            healthScript.ForcePassThreshold(); 
        }

        nextAttackTime = Time.time + Random.Range(minAttackDelay, maxAttackDelay);
    }
}