using UnityEngine;

/// <summary>
/// Handles 2D physics-based player movement, including walking, jumping, 
/// ground detection, and animation synchronization. Integrates with the combat 
/// system to restrict movement during actions.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerMovements : MonoBehaviour
{
    [Header("Movement Configuration")]
    [Tooltip("The maximum horizontal movement speed of the player.")]
    public float moveSpeed = 5f;

    [Header("Ground Detection")]
    [Tooltip("A Transform positioned at the character's feet to detect the ground.")]
    public Transform groundCheck;
    
    [Tooltip("The radius of the invisible circle used to detect ground collisions.")]
    public float groundCheckRadius = 0.3f;
    
    [Tooltip("The physics layer(s) considered as solid ground.")]
    public LayerMask groundLayer;
    
    public bool isGrounded;

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private PlayerCombat combatScript;

    private Vector2 movement;

    /// <summary>
    /// Caches internal component references on initialization.
    /// </summary>
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        combatScript = GetComponent<PlayerCombat>();
    }

    /// <summary>
    /// Processes frame-by-frame input, environment checks, and animation state updates.
    /// </summary>
    void Update()
    {
        // Performs a circular overlap check to determine if the player is standing on the ground layer
        if (groundCheck != null)
        {
            isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        }

        // Update animator parameters for locomotion and airborne states
        if (animator != null)
        {
            animator.SetBool("isGrounded", isGrounded);
            animator.SetFloat("yVelocity", rb.linearVelocity.y);
        }

        // Action Lock: If the player is currently attacking, freeze horizontal input and lock rotation to the enemy
        if (combatScript != null && combatScript.isAttacking)
        {
            movement.x = 0;
            
            if (animator != null) 
            {
                animator.SetFloat("Speed", 0);
            }
            
            AutoFaceClosestEnemy();
            return; 
        }

        // Capture raw horizontal input (A/D or Left/Right arrows)
        movement.x = Input.GetAxisRaw("Horizontal");

        // Flip character sprite based on the current movement direction via transform scale
        if (movement.x > 0)
        {
            transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        }
        else if (movement.x < 0)
        {
            transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        }

        if (animator != null)
        {
            animator.SetFloat("Speed", Mathf.Abs(movement.x));
        }

        // Jump logic: applies a vertical impulse if the player is grounded and jump button is pressed
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 10f);
            PlayerAudio pAudio = GetComponent<PlayerAudio>();
            if (pAudio != null)
            {
                pAudio.PlaySound(pAudio.jumpSound);
            }
        }
    }

    /// <summary>
    /// Automatically rotates the player's scale to face the nearest active enemy.
    /// Used during combat frames to ensure hits connect even if the player releases input.
    /// </summary>
    private void AutoFaceClosestEnemy()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        GameObject closestEnemy = null;
        float closestDistance = Mathf.Infinity; 

        foreach (GameObject enemy in enemies)
        {
            // Ignore inactive enemies to prevent focusing on dead or disabled targets
            if (!enemy.activeInHierarchy) continue; 

            float dist = Vector2.Distance(transform.position, enemy.transform.position);
            if (dist < closestDistance)
            {
                closestDistance = dist;
                closestEnemy = enemy;
            }
        }

        if (closestEnemy != null)
        {
            // Determine side and flip localScale to face the target's X position
            if (closestEnemy.transform.position.x > transform.position.x)
            {
                transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
            }
            else
            {
                transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
            }
        }
    }

    /// <summary>
    /// Handles physical velocity application synchronized with the fixed physics step.
    /// Locks horizontal velocity during combat actions while allowing gravity to persist.
    /// </summary>
    void FixedUpdate()
    {
        if (combatScript != null && (combatScript.isAttacking || combatScript.isDefending))
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            return;
        }

        rb.linearVelocity = new Vector2(movement.x * moveSpeed, rb.linearVelocity.y);
    }
}