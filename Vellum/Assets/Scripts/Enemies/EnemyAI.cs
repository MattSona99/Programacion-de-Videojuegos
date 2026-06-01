using UnityEngine;

/// <summary>
/// Arena enemy AI for Act_02 (simple movement, no NavMesh). Dynamic targeting via
/// EnemyTargetCoordinator: chases Jammo by default, switches to the Player if hit by them and
/// a Player slot is free. The attack rate toward the target is driven by a fuzzy-logic
/// controller (EnemyFuzzyBrain). Attackers of the same target spread out in a FAN over angular
/// slots (coordinator) so they don't queue up; separation refines the steering.
/// Implements IDamageReaction to notice being hit by the Player. Requires Health on the same
/// GameObject; KnockbackReceiver is optional.
/// </summary>
[RequireComponent(typeof(Health))]
public class EnemyAI : MonoBehaviour, IDamageReaction
{
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int DeadHash = Animator.StringToHash("Dead");

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float turnSpeed = 10f;
    [SerializeField] private float gravity = -20f;

    [Header("Separation (anti-overlap, independent movement)")]
    [Tooltip("Enemy layer: used to steer away from nearby peers and to count allies (fuzzy).")]
    [SerializeField] private LayerMask enemyMask;
    [SerializeField] private float separationRadius = 1.5f;
    [SerializeField] private float separationWeight = 1.5f;
    [Tooltip("Radius within which to count allies (the fuzzy 'crowding' input).")]
    [SerializeField] private float peerCheckRadius = 2.5f;

    [Header("Attack / contact")]
    [SerializeField] private float attackRange = 1.8f;
    [Tooltip("Desired distance at which the enemy STOPS advancing and stays close to the target. A safety minimum is still enforced from the real collider radii (see collisionMargin), so it never bumps into the target. It keeps following the target if it moves away.")]
    [SerializeField] private float stopDistance = 1f;
    [Tooltip("Margin added to the sum of the capsule radii for the minimum safety stop distance (anti-jam against the target's collider).")]
    [SerializeField] private float collisionMargin = 0.2f;
    [Tooltip("Cooldown at MINIMUM aggression (fuzzy aggression = 0).")]
    [SerializeField] private float maxAttackCooldown = 3f;
    [Tooltip("Cooldown at MAXIMUM aggression (fuzzy aggression = 1).")]
    [SerializeField] private float minAttackCooldown = 1f;
    [SerializeField] private float enemyDamage = 10f;

    [Header("Encirclement (fan effect)")]
    [Tooltip("Angular spacing (degrees) between one slot and the next around the target.")]
    [SerializeField] private float fanSpacingDeg = 32f;

    [Header("Fuzzy decision")]
    [Tooltip("How often (seconds) to re-evaluate the fuzzy decision (not every frame: no allocations in a hot path).")]
    [SerializeField] private float decisionInterval = 0.2f;

    [Header("Targeting")]
    [Tooltip("How often (seconds) to re-evaluate the target (Player vs Jammo).")]
    [SerializeField] private float retargetInterval = 0.3f;

    private Health _health;
    private CharacterController _controller;
    private Animator _animator;
    private KnockbackReceiver _knockback;
    private readonly AnimatorParameterCache _animParams = new AnimatorParameterCache();
    private readonly Collider[] _separationBuffer = new Collider[8];

    private EnemyFuzzyBrain _brain;
    private EnemyDecision _decision;
    private float _decisionTimer;
    private float _allyCount;

    private Transform _currentTarget;
    private Health _currentTargetHealth;
    private bool _triggeredByPlayer;
    private float _retargetTimer;
    private Transform _fallbackPlayer;
    private bool _fallbackTried;

    private bool _deathHandled;
    private float _lastAttackTime = float.NegativeInfinity;
    private float _currentAttackCooldown = 0f;
    private float _verticalVelocity;
    private float _ownRadius = 0.5f;
    private float _targetRadius;

    void Awake()
    {
        _health = GetComponent<Health>();
        _controller = GetComponent<CharacterController>();
        if (_controller == null)
            Debug.LogWarning("[EnemyAI] No CharacterController: the enemy won't collide with the arena walls.");
        else
            _ownRadius = _controller.radius;
        _knockback = GetComponent<KnockbackReceiver>();
        _animator = GetComponentInChildren<Animator>();
        _animParams.Refresh(_animator);
        _brain = new EnemyFuzzyBrain();
    }

    // EFFECTIVE stop distance: never below the sum of the capsule radii
    // (enemy + target) + margin, so the enemy doesn't bump into the target's
    // collider (no invisible wall / repulsive push).
    private float EffectiveStop() => Mathf.Max(stopDistance, _ownRadius + _targetRadius + collisionMargin);

    void OnDisable()
    {
        // Out of the coordinator's lists (pooling): release the slot.
        if (EnemyTargetCoordinator.Instance != null) EnemyTargetCoordinator.Instance.Unregister(this);
    }

    /// <summary>
    /// IDamageReaction: called by Health on every hit taken. If the hit comes from the Player,
    /// the enemy is "triggered" and tries to engage them immediately.
    /// </summary>
    public void OnDamaged(DamageInfo info)
    {
        if (info.source != null && info.source.CompareTag("Player"))
        {
            _triggeredByPlayer = true;
            var coord = EnemyTargetCoordinator.Instance;
            if (coord != null && coord.JammoAlive && coord.PlayerAlive && coord.TryClaimPlayer(this))
            {
                _currentTarget = coord.Player;
                _currentTargetHealth = coord.PlayerHealth;
            }
        }
    }

    void Update()
    {
        if (_health != null && _health.IsDead)
        {
            if (!_deathHandled) HandleDeath();
            return;
        }

        if (_knockback != null && _knockback.IsKnockbackActive) return;

        _retargetTimer -= Time.deltaTime;
        if (_retargetTimer <= 0f || _currentTarget == null ||
            (_currentTargetHealth != null && _currentTargetHealth.IsDead))
        {
            _retargetTimer = retargetInterval;
            ResolveTarget();
        }

        if (_currentTarget == null)
        {
            SetAnimFloat(SpeedHash, 0f);
            if (_controller != null) MoveStep(Vector3.zero);
            return;
        }

        Vector3 toTarget = _currentTarget.position - transform.position;
        toTarget.y = 0f;
        float planarDist = toTarget.magnitude;

        TickFuzzy(planarDist);

        if (planarDist <= 0.001f)
        {
            if (_controller != null) MoveStep(Vector3.zero);
            return;
        }

        Vector3 dir = toTarget / planarDist;

        // Standoff point around the target (at stopDistance), on an angular slot:
        // with multiple attackers they fan out instead of queuing up.
        Vector3 approach = ComputeApproachPoint(_currentTarget.position, dir);
        Vector3 toApproach = approach - transform.position;
        toApproach.y = 0f;
        Vector3 moveDir = toApproach.sqrMagnitude > 1e-4f ? toApproach.normalized : dir;

        // Attack as soon as the target is in range, even while taking the last step.
        if (planarDist <= attackRange) { FaceDirection(dir); TryAttack(_currentTarget); }
        else { FaceDirection(moveDir); }

        // Gate on the distance TO THE TARGET (not to the point): stops advancing as soon
        // as it's close, so it doesn't push against the target's collider (no invisible
        // wall / repulsive force). Resumes if the target moves beyond the effective stop.
        if (planarDist > EffectiveStop())
        {
            Vector3 steer = (moveDir + ComputeSeparation()).normalized;
            MoveStep(steer);
            SetAnimFloat(SpeedHash, 1f);
        }
        else
        {
            MoveStep(Vector3.zero);
            SetAnimFloat(SpeedHash, 0f);
        }
    }

    /// <summary>Picks Player or Jammo according to the coordinator's rules.</summary>
    private void ResolveTarget()
    {
        var coord = EnemyTargetCoordinator.Instance;
        if (coord == null)
        {
            // Fallback (coordinator not in scene): target the Player, so the enemies
            // don't stand still during a playtest before the wiring is done.
            if (!_fallbackTried)
            {
                _fallbackTried = true;
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) _fallbackPlayer = p.transform;
            }
            if (_fallbackPlayer != null)
                SetTarget(_fallbackPlayer, _fallbackPlayer.GetComponentInParent<Health>());
            return;
        }

        // Jammo dead = level over: no target, the enemies stand still.
        if (!coord.JammoAlive)
        {
            SetTarget(null, null);
            return;
        }

        bool playerAlive = coord.PlayerAlive;

        // If it's already chasing the Player (reserved slot), keep going while they live.
        if (coord.IsChasingPlayer(this) && playerAlive)
        {
            SetTarget(coord.Player, coord.PlayerHealth);
            return;
        }

        // Triggered by the Player and a slot is free → engage the Player.
        if (_triggeredByPlayer && playerAlive && coord.TryClaimPlayer(this))
        {
            SetTarget(coord.Player, coord.PlayerHealth);
            return;
        }

        // Default: Jammo (alive, guaranteed by the check above).
        SetTarget(coord.Jammo, coord.JammoHealth);
    }

    private void SetTarget(Transform t, Health h)
    {
        _currentTarget = t;
        _currentTargetHealth = h;

        // Radius of the target's collider (for the safety stop distance).
        // SetTarget runs on a timer, not every frame → GetComponent is acceptable.
        CharacterController tc = t != null ? t.GetComponentInParent<CharacterController>() : null;
        _targetRadius = tc != null ? tc.radius : 0f;

        var coord = EnemyTargetCoordinator.Instance;
        if (coord == null) return;

        if (t != null && t == coord.Jammo) coord.RegisterJammo(this);
        else if (t != null && t == coord.Player) { /* already in the list via TryClaimPlayer */ }
        else coord.Unregister(this);
    }

    /// <summary>
    /// Standoff point at 'stopDistance' from the target, on the side the enemy approaches from.
    /// With multiple attackers of the same target, each rotates by one angular slot → they fan
    /// out around the target (those behind aim for a free side slot and circle around).
    /// </summary>
    private Vector3 ComputeApproachPoint(Vector3 targetPos, Vector3 dir)
    {
        Vector3 fromTarget = -dir; // target → enemy

        var coord = EnemyTargetCoordinator.Instance;
        if (coord != null)
        {
            bool isPlayer = _currentTarget == coord.Player;
            int count = coord.AttackerCount(isPlayer);
            int slot = coord.SlotIndex(this, isPlayer);
            if (count > 1 && slot >= 0)
            {
                float angle = (slot - (count - 1) * 0.5f) * fanSpacingDeg;
                fromTarget = Quaternion.AngleAxis(angle, Vector3.up) * fromTarget;
            }
        }

        return targetPos + fromTarget * EffectiveStop();
    }

    private void TickFuzzy(float planarDist)
    {
        _decisionTimer -= Time.deltaTime;
        if (_decisionTimer > 0f) return;
        _decisionTimer = decisionInterval;

        _allyCount = CountNearbyAllies();

        var perception = new EnemyPerception
        {
            distance = planarDist,
            healthPct = _health != null ? _health.Normalized : 1f,
            allyCount = _allyCount
        };
        _decision = _brain.Decide(perception);
    }

    private float CountNearbyAllies()
    {
        int n = Physics.OverlapSphereNonAlloc(transform.position, peerCheckRadius,
                                              _separationBuffer, enemyMask);
        int allies = 0;
        for (int i = 0; i < n; i++)
        {
            Transform other = _separationBuffer[i].transform;
            if (other == null || other.root == transform.root) continue;
            Health oh = other.GetComponentInParent<Health>();
            if (oh != null && oh.IsDead) continue;
            allies++;
        }
        return allies;
    }

    private void FaceDirection(Vector3 dir)
    {
        if (dir.sqrMagnitude < 1e-6f) return;
        Quaternion lookRot = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, turnSpeed * Time.deltaTime);
    }

    private Vector3 ComputeSeparation()
    {
        int n = Physics.OverlapSphereNonAlloc(transform.position, separationRadius,
                                               _separationBuffer, enemyMask);
        Vector3 push = Vector3.zero;
        for (int i = 0; i < n; i++)
        {
            Transform other = _separationBuffer[i].transform;
            if (other == null || other.root == transform.root) continue;

            Health otherHealth = other.GetComponentInParent<Health>();
            if (otherHealth != null && otherHealth.IsDead) continue;

            Vector3 away = transform.position - other.position;
            away.y = 0f;
            float d = away.magnitude;
            if (d > 0.0001f) push += away / d / d;
        }
        return push * separationWeight;
    }

    private void MoveStep(Vector3 horizontalDir)
    {
        if (_controller != null && _controller.enabled)
        {
            if (_controller.isGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f;
            _verticalVelocity += gravity * Time.deltaTime;

            Vector3 motion = horizontalDir * moveSpeed + Vector3.up * _verticalVelocity;
            _controller.Move(motion * Time.deltaTime);
        }
        else
        {
            transform.position += horizontalDir * moveSpeed * Time.deltaTime;
        }
    }

    private void TryAttack(Transform target)
    {
        if (Time.time - _lastAttackTime < _currentAttackCooldown) return;

        _lastAttackTime = Time.time;
        // Fuzzy aggression: high → short cooldown (attacks often), low → long.
        float baseCd = Mathf.Lerp(maxAttackCooldown, minAttackCooldown, Mathf.Clamp01(_decision.aggression));
        _currentAttackCooldown = Mathf.Clamp(baseCd * Random.Range(0.85f, 1.15f),
                                             minAttackCooldown, maxAttackCooldown);

        SetAnimTrigger(AttackHash);

        IDamageable victim = target.GetComponentInParent<IDamageable>();
        if (victim != null)
            victim.TakeDamage(new DamageInfo(enemyDamage, transform.position, gameObject));
    }

    private void HandleDeath()
    {
        _deathHandled = true;
        if (EnemyTargetCoordinator.Instance != null) EnemyTargetCoordinator.Instance.Unregister(this);
        SetAnimFloat(SpeedHash, 0f);
        SetAnimTrigger(DeadHash);
        if (_knockback != null) _knockback.CancelKnockback();
    }

    private void SetAnimFloat(int hash, float value)
    {
        if (_animator != null && _animParams.Has(hash)) _animator.SetFloat(hash, value);
    }

    private void SetAnimTrigger(int hash)
    {
        if (_animator != null && _animParams.Has(hash)) _animator.SetTrigger(hash);
    }

    /// <summary>Pooling hook: alias for <see cref="ResetState"/>, called when the enemy is spawned/reused.</summary>
    public void Configure() => ResetState();

    /// <summary>Resets all runtime state so the pooled enemy starts clean (targeting, fuzzy, animator, controller).</summary>
    public void ResetState()
    {
        _deathHandled = false;
        _lastAttackTime = float.NegativeInfinity;
        _currentAttackCooldown = 0f;
        _verticalVelocity = 0f;

        // Targeting / fuzzy: start clean (the GameObject is reused from the pool).
        _triggeredByPlayer = false;
        _currentTarget = null;
        _currentTargetHealth = null;
        _decision = default;
        _decisionTimer = 0f;
        _retargetTimer = 0f;
        if (EnemyTargetCoordinator.Instance != null) EnemyTargetCoordinator.Instance.Unregister(this);

        if (_knockback != null) _knockback.CancelKnockback();

        if (_animator != null)
        {
            _animator.Rebind();
            _animator.Update(0f);
        }

        if (_controller != null)
        {
            _controller.enabled = false;
            _controller.enabled = true;
        }
    }
}
