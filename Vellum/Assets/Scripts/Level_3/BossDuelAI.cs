using System.Collections;
using UnityEngine;

/// <summary>
/// AI for the final-level doppelganger. Separate from EnemyAI (the wave arena): here there's a
/// single boss, no coordinator/fan. Locomotion via CharacterController (no NavMesh).
///
/// Windowed combat pacing, to avoid being monotonous:
///   - AGGRO: chases the target and, in range, performs a COMBO (several hits in a row, like the
///     Player's combo). Stays still during the combo (no slide).
///   - REPOSITION: runs to a random point to reposition.
///   - GUARD: on arrival, raises the guard WHILE STILL and waits for the Player; then decides
///     whether to attack or reposition again.
/// On top of all: REACTIVE DEFENSE — when it sees the Player's swing start
/// (PlayerMeleeAttack.IsSwinging) and is close and facing, it raises the guard probabilistically
/// to block that hit (not always).
///
/// Walks AND runs: the Animator (a clone of the Player) has a walk/run blend tree on 'Speed'
/// (real m/s) + 'MotionSpeed'. Approach = walk, reposition = run.
///
/// Phase 2 (Moon): empowered; can BREAK OFF from the Player to intercept a carrying Jammo
/// (BossFuzzyBrain). The director pauses the boss during flips and in the final-hit window
/// (SetPaused). No KnockbackReceiver: the boss fights planted.
/// </summary>
[RequireComponent(typeof(Health))]
public class BossDuelAI : MonoBehaviour
{
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int MotionSpeedHash = Animator.StringToHash("MotionSpeed");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int IsDefendingHash = Animator.StringToHash("IsDefending");
    private static readonly int DeadHash = Animator.StringToHash("Dead");

    private enum State { Aggro, Reposition, Guard, Kite, SeekHealth }

    [Header("Targets")]
    [SerializeField] private Transform player;
    [SerializeField] private JammoCarrier jammo;
    [Tooltip("The Player's PlayerMeleeAttack (for reactive defense). If empty, found among the Player's children.")]
    [SerializeField] private PlayerMeleeAttack playerMelee;

    [Header("Movement (walk/run)")]
    [SerializeField] private float walkSpeed = 2f;
    [SerializeField] private float runSpeed = 4.5f;
    [Tooltip("Beyond this distance from the target it runs instead of walking.")]
    [SerializeField] private float runDistance = 4f;
    [SerializeField] private float turnSpeed = 10f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float stopDistance = 1.6f;

    [Header("Attack / combo")]
    [SerializeField] private float attackRange = 1.9f;
    [Tooltip("Number of combo hits (= the Animator's 4-hit combo).")]
    [SerializeField] private int comboLength = 4;
    [Tooltip("Interval between one combo hit and the next.")]
    [SerializeField] private float comboHitInterval = 0.28f;
    [Tooltip("Recovery after a combo before another can be done.")]
    [SerializeField] private float comboRecovery = 1.2f;
    [SerializeField] private float attackDamage = 12f;

    [Header("Attack/defense windows")]
    [Tooltip("Duration of the attack window before repositioning.")]
    [SerializeField] private float aggroWindow = 5f;
    [Tooltip("Maximum wait in guard before re-deciding.")]
    [SerializeField] private float guardWindow = 3f;
    [Tooltip("Radius around the Player within which to pick the reposition point.")]
    [SerializeField] private float repositionRadius = 6f;
    [SerializeField] private float repositionArriveDistance = 0.6f;
    [Tooltip("Minimum distance of the reposition point from the boss's CURRENT position: avoids 'arriving' instantly and cycling Reposition→Guard→Reposition in place.")]
    [SerializeField] private float repositionMinDistance = 3.5f;
    [Tooltip("Maximum time to reach the point (anti-stall).")]
    [SerializeField] private float repositionTimeout = 4f;
    [Tooltip("Distance at which, in guard, the Player has 'arrived' and the boss re-decides.")]
    [SerializeField] private float engageDistance = 3f;
    [Range(0f, 1f)]
    [Tooltip("Probability of switching to attack (rather than repositioning again) when leaving guard.")]
    [SerializeField] private float attackAfterGuardChance = 0.7f;

    [Header("Kiting (requires an upper-body Animator layer, otherwise it slides)")]
    [Range(0f, 1f)]
    [Tooltip("Probability of kiting (circling at high guard) instead of stationary repositioning at the end of the attack window. Keep 0 until you have the masked layer.")]
    [SerializeField] private float kiteChance = 0f;
    [SerializeField] private float kiteWindow = 4f;
    [Tooltip("Distance from the Player the boss tries to keep while kiting.")]
    [SerializeField] private float kiteDistance = 4f;
    [SerializeField] private float kiteSpeed = 2.5f;
    [Tooltip("How often (seconds) it flips the rotation direction around the Player.")]
    [SerializeField] private float kiteFlipInterval = 1.5f;

    [Header("Reactive defense")]
    [Range(0f, 1f)]
    [Tooltip("Probability of raising the guard when it sees the Player's swing start.")]
    [SerializeField] private float reactiveBlockChance = 0.45f;
    [SerializeField] private float reactiveBlockTime = 0.6f;
    [Tooltip("Maximum distance at which it reacts to the Player's swing.")]
    [SerializeField] private float reactiveBlockRange = 2.6f;

    [Header("Phase 2 (Moon) empowerment")]
    [Tooltip("Walk/run multiplier when in Moon phase.")]
    [SerializeField] private float moonSpeedMultiplier = 1.3f;
    [SerializeField] private float moonAttackDamage = 18f;
    [SerializeField] private float moonComboRecovery = 0.7f;

    [Header("Fuzzy decision (Phase 2 only)")]
    [SerializeField] private float decisionInterval = 0.2f;
    [Range(0f, 1f)]
    [SerializeField] private float interceptThreshold = 0.5f;
    [Tooltip("Beyond this distance the boss DROPS the Jammo intercept (hysteresis: once locked on, it chases while Jammo is carrying and closer than this).")]
    [SerializeField] private float interceptGiveUpDistance = 12f;

    [Header("Health recovery (neutral pickups)")]
    [Tooltip("The duel's health pickup spawner. If empty, the boss never seeks health.")]
    [SerializeField] private DuelHealthSpawner healthSpawner;
    [Range(0f, 1f)]
    [Tooltip("Fuzzy threshold above which the boss breaks off to go heal.")]
    [SerializeField] private float seekHealthThreshold = 0.55f;
    [Tooltip("Delay before the boss 'notices' a just-appeared pickup (head start for the Player).")]
    [SerializeField] private float pickupReactionDelay = 1.5f;
    [Tooltip("Speed at which it runs toward the pickup.")]
    [SerializeField] private float seekHealthSpeed = 4.5f;

    private Health _health;
    private CharacterController _controller;
    private Animator _animator;
    private BossShield _shield;
    private readonly AnimatorParameterCache _animParams = new AnimatorParameterCache();

    private BossFuzzyBrain _brain;
    private float _decisionTimer;
    private bool _interceptJammo;

    private bool _moonPhase;
    private bool _paused;
    private bool _deathHandled;
    private float _verticalVelocity;

    private State _state;
    private float _stateTimer;
    private bool _lastWasReposition;
    private Transform _healthTarget;
    private float _seekDecisionTimer;
    private Vector3 _repositionPoint;
    private float _strafeSign = 1f;
    private float _flipTimer;
    private Coroutine _comboRoutine;
    private float _comboCooldownUntil;
    private float _reactiveBlockUntil;
    private bool _prevPlayerSwinging;
    private bool _guardApplied;

    private float CurrentAttackDamage => _moonPhase ? moonAttackDamage : attackDamage;
    private float CurrentComboRecovery => _moonPhase ? moonComboRecovery : comboRecovery;
    private float SpeedMul => _moonPhase ? moonSpeedMultiplier : 1f;

    void Awake()
    {
        _health = GetComponent<Health>();
        _controller = GetComponent<CharacterController>();
        _shield = GetComponent<BossShield>();
        _brain = new BossFuzzyBrain();
        if (playerMelee == null && player != null) playerMelee = player.GetComponentInChildren<PlayerMeleeAttack>();
        RefreshAnimatorIfStale();
        EnterState(State.Aggro);
    }

    /// <summary>Switches the boss to Moon phase (empowered) or back to Sun phase.</summary>
    public void SetMoonPhase(bool moon) => _moonPhase = moon;

    /// <summary>Pauses/resumes the boss (used by the director during flips and the final-hit window). On pause it cancels combo, drops guard, and stops.</summary>
    public void SetPaused(bool paused)
    {
        _paused = paused;
        if (paused)
        {
            CancelCombo();
            ApplyGuard(false);
            SetAnimFloat(SpeedHash, 0f);
            if (_controller != null && _controller.enabled) _controller.Move(Vector3.zero);
        }
    }

    void Update()
    {
        if (_health != null && _health.IsDead)
        {
            if (!_deathHandled) HandleDeath();
            return;
        }

        if (_paused) return;

        // --- Reactive defense: rising edge of the Player's swing ---
        DetectReactiveBlock();

        bool reactiveActive = Time.time < _reactiveBlockUntil;

        // During a combo or a reactive block the boss stays still (no slide):
        // only gravity + facing toward the Player.
        if (_comboRoutine != null || reactiveActive)
        {
            ApplyGuard(reactiveActive); // the combo does NOT raise the guard; the reactive block does
            if (player != null) FaceDirection(Planar(player.position - transform.position));
            SetAnimFloat(SpeedHash, 0f);
            MoveStep(Vector3.zero, 0f);
            return;
        }

        // Top priority: if wounded and there's a ready pickup nearby, go heal.
        MaybeDecideSeekHealth();

        Transform target = ResolveTarget();
        if (target == null && _state != State.SeekHealth)
        {
            ApplyGuard(false);
            SetAnimFloat(SpeedHash, 0f);
            MoveStep(Vector3.zero, 0f);
            return;
        }

        switch (_state)
        {
            case State.Aggro: TickAggro(target); break;
            case State.Reposition: TickReposition(); break;
            case State.Guard: TickGuard(); break;
            case State.Kite: TickKite(); break;
            case State.SeekHealth: TickSeekHealth(); break;
        }
    }

    // ---- States -----------------------------------------------------------

    private void TickAggro(Transform target)
    {
        ApplyGuard(false);

        Vector3 to = Planar(target.position - transform.position);
        float dist = to.magnitude;
        Vector3 dir = dist > 0.001f ? to / dist : Vector3.zero;
        FaceDirection(dir);

        if (dist <= attackRange && Time.time >= _comboCooldownUntil)
        {
            _comboRoutine = StartCoroutine(ComboRoutine(target));
            return;
        }

        if (dist > stopDistance)
        {
            float speed = dist > runDistance ? runSpeed : walkSpeed;
            MoveAt(dir, speed);
        }
        else
        {
            SetAnimFloat(SpeedHash, 0f);
            MoveStep(Vector3.zero, 0f);
        }

        _stateTimer -= Time.deltaTime;
        if (_stateTimer <= 0f)
            EnterState(Random.value < kiteChance ? State.Kite : State.Reposition);
    }

    private void TickReposition()
    {
        ApplyGuard(false);

        Vector3 to = Planar(_repositionPoint - transform.position);
        float dist = to.magnitude;
        _stateTimer -= Time.deltaTime;

        if (dist <= repositionArriveDistance || _stateTimer <= 0f)
        {
            EnterState(State.Guard);
            return;
        }

        Vector3 dir = to / Mathf.Max(dist, 0.001f);
        FaceDirection(dir);
        MoveAt(dir, runSpeed); // reposition = run
    }

    private void TickGuard()
    {
        ApplyGuard(true);
        SetAnimFloat(SpeedHash, 0f);
        MoveStep(Vector3.zero, 0f);

        if (player != null) FaceDirection(Planar(player.position - transform.position));

        _stateTimer -= Time.deltaTime;
        float playerDist = player != null ? Planar(player.position - transform.position).magnitude : Mathf.Infinity;

        if (playerDist <= engageDistance || _stateTimer <= 0f)
        {
            // No two repositions in a row: if we got here from a Reposition, go back to
            // attacking (avoids passive wandering).
            if (_lastWasReposition || Random.value < attackAfterGuardChance) EnterState(State.Aggro);
            else EnterState(State.Reposition);
        }
    }

    /// <summary>Kiting: circles the Player at high guard keeping kiteDistance, flipping direction periodically. Requires the upper-body Animator layer (otherwise it slides). Returns to attacking at the end.</summary>
    private void TickKite()
    {
        ApplyGuard(true);

        if (player == null) { EnterState(State.Aggro); return; }

        Vector3 to = Planar(player.position - transform.position);
        float dist = to.magnitude;
        Vector3 toPlayer = dist > 0.001f ? to / dist : transform.forward;
        FaceDirection(toPlayer);

        _flipTimer -= Time.deltaTime;
        if (_flipTimer <= 0f) { _strafeSign = -_strafeSign; _flipTimer = kiteFlipInterval; }

        Vector3 strafe = Vector3.Cross(Vector3.up, toPlayer) * _strafeSign;
        float radial = Mathf.Clamp(dist - kiteDistance, -1f, 1f); // >0 too far (move closer), <0 too close (move away)
        Vector3 dir = (strafe + toPlayer * radial).normalized;

        MoveAt(dir, kiteSpeed);

        _stateTimer -= Time.deltaTime;
        if (_stateTimer <= 0f) EnterState(State.Aggro);
    }

    private void EnterState(State next)
    {
        _state = next;
        _lastWasReposition = next == State.Reposition;
        switch (next)
        {
            case State.Aggro:
                _stateTimer = aggroWindow;
                break;
            case State.Reposition:
                _repositionPoint = PickRepositionPoint();
                _stateTimer = repositionTimeout;
                break;
            case State.Guard:
                _stateTimer = guardWindow;
                break;
            case State.Kite:
                _stateTimer = kiteWindow;
                _strafeSign = Random.value < 0.5f ? -1f : 1f;
                _flipTimer = kiteFlipInterval;
                break;
            case State.SeekHealth:
                _stateTimer = repositionTimeout; // anti-stall safety
                break;
        }
    }

    private Vector3 PickRepositionPoint()
    {
        Vector3 around = player != null ? player.position : transform.position;
        Vector3 self = transform.position;
        Vector3 p = self;

        // Sample a point on the circle around the Player that is at least
        // repositionMinDistance from the boss's current position (so it actually moves,
        // no instant arrival).
        for (int attempt = 0; attempt < 6; attempt++)
        {
            float angle = Random.value * Mathf.PI * 2f;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * repositionRadius;
            p = around + offset;
            p.y = self.y;
            if (Planar(p - self).magnitude >= repositionMinDistance) return p;
        }
        return p; // fallback: the last sampled
    }

    // ---- Cinematic walk-back (driven by the director) ---------------------

    /// <summary>
    /// Walks the boss back to <paramref name="pos"/> at walking pace. Used by
    /// <see cref="MirrorDuelDirector"/> during the phase transition: while the boss is paused
    /// (<see cref="Update"/> early-returns on <c>_paused</c>, so there's no conflict) the director
    /// drives this coroutine to send the actors back to their spawn while the camera rises. Feeds
    /// the real speed to the Animator so it walks without sliding; on arrival it faces the Player
    /// again and re-enters <see cref="State.Aggro"/>, so the next phase starts from a clean state.
    /// </summary>
    public IEnumerator ReturnToSpawn(Vector3 pos)
    {
        const float arriveDist = 0.2f;
        float timeout = repositionTimeout * 2f; // anti-stall safety

        while (timeout > 0f)
        {
            Vector3 to = Planar(pos - transform.position);
            float dist = to.magnitude;
            if (dist <= arriveDist) break;

            Vector3 dir = to / Mathf.Max(dist, 0.001f);
            FaceDirection(dir);
            MoveAt(dir, runSpeed); // run back to spawn (reposition cinematic)

            timeout -= Time.deltaTime;
            yield return null;
        }

        SetAnimFloat(SpeedHash, 0f);
        MoveStep(Vector3.zero, 0f);
        if (player != null) FaceDirection(Planar(player.position - transform.position));
        EnterState(State.Aggro);
    }

    // ---- Combo ------------------------------------------------------------

    private IEnumerator ComboRoutine(Transform target)
    {
        for (int i = 0; i < comboLength; i++)
        {
            if (_health.IsDead || _paused) break;

            if (target != null) FaceDirection(Planar(target.position - transform.position));
            SetAnimTrigger(AttackHash);

            yield return new WaitForSeconds(comboHitInterval * 0.5f);

            if (target != null && InAttackReach(target))
            {
                IDamageable victim = target.GetComponentInParent<IDamageable>();
                if (victim != null)
                    victim.TakeDamage(new DamageInfo(CurrentAttackDamage, transform.position, gameObject));
            }

            yield return new WaitForSeconds(comboHitInterval * 0.5f);
        }

        _comboCooldownUntil = Time.time + CurrentComboRecovery;
        _comboRoutine = null;
    }

    private void CancelCombo()
    {
        if (_comboRoutine != null) { StopCoroutine(_comboRoutine); _comboRoutine = null; }
    }

    private bool InAttackReach(Transform t)
    {
        Vector3 to = Planar(t.position - transform.position);
        if (to.magnitude > attackRange + 0.3f) return false;
        return Vector3.Angle(transform.forward, to) <= 70f;
    }

    // ---- Difesa reattiva --------------------------------------------------

    private void DetectReactiveBlock()
    {
        bool swinging = playerMelee != null && playerMelee.IsSwinging;
        bool rising = swinging && !_prevPlayerSwinging;
        _prevPlayerSwinging = swinging;

        if (!rising || _comboRoutine != null || player == null) return;

        Vector3 to = Planar(player.position - transform.position);
        if (to.magnitude > reactiveBlockRange) return;
        if (Vector3.Angle(transform.forward, to) > 90f) return; // the Player must be in front

        if (Random.value < reactiveBlockChance)
            _reactiveBlockUntil = Time.time + reactiveBlockTime;
    }

    // ---- Target (fuzzy in Phase 2) ---------------------------------------

    /// <summary>Resolves the current target: the Player, or (Moon phase, via fuzzy + hysteresis) a carrying Jammo to intercept.</summary>
    private Transform ResolveTarget()
    {
        if (_moonPhase && jammo != null && !jammo.IsDead && jammo.IsCarrying)
        {
            float jammoDist = Planar(jammo.transform.position - transform.position).magnitude;

            _decisionTimer -= Time.deltaTime;
            if (_decisionTimer <= 0f)
            {
                _decisionTimer = decisionInterval;
                // The fuzzy ENABLES the intercept; the hysteresis below turns it off
                // (no flip-flop at the threshold edge every 0.2 s).
                if (!_interceptJammo && _brain.Intercept(jammoDist, true) >= interceptThreshold)
                    _interceptJammo = true;
            }

            // Hysteresis: once locked on, it chases Jammo while he's carrying and not
            // too far. If he gets beyond the threshold, it drops the chase.
            if (_interceptJammo && jammoDist <= interceptGiveUpDistance)
                return jammo.transform;
        }

        _interceptJammo = false;
        return player;
    }

    // ---- Health recovery (fuzzy decision gated on HP) --------------------

    /// <summary>Decides whether to break off to heal: only if wounded and with a "ready" pickup (alive at least pickupReactionDelay) nearby. Once in SeekHealth it stays (hysteresis) while the pickup exists.</summary>
    private void MaybeDecideSeekHealth()
    {
        if (_state == State.SeekHealth) return;
        if (healthSpawner == null || _health == null) return;

        _seekDecisionTimer -= Time.deltaTime;
        if (_seekDecisionTimer > 0f) return;
        _seekDecisionTimer = decisionInterval;

        if (!healthSpawner.TryGetNearestReady(transform.position, pickupReactionDelay, out Transform p, out float dist))
            return;

        if (_brain.SeekHealth(_health.Normalized, dist) >= seekHealthThreshold)
        {
            _healthTarget = p;
            EnterState(State.SeekHealth);
        }
    }

    private void TickSeekHealth()
    {
        ApplyGuard(false);

        // Pickup collected/expired (deactivated by the pool) → back to fighting.
        if (_healthTarget == null || !_healthTarget.gameObject.activeInHierarchy)
        {
            _healthTarget = null;
            EnterState(State.Aggro);
            return;
        }

        Vector3 to = Planar(_healthTarget.position - transform.position);
        float dist = to.magnitude;
        Vector3 dir = dist > 0.001f ? to / dist : Vector3.zero;
        FaceDirection(dir);
        MoveAt(dir, seekHealthSpeed);

        // Healing happens on its own by walking over the pickup (trigger). Anti-stall safety:
        _stateTimer -= Time.deltaTime;
        if (_stateTimer <= 0f) { _healthTarget = null; EnterState(State.Aggro); }
    }

    // ---- Locomotion / animation ------------------------------------------

    private static Vector3 Planar(Vector3 v) { v.y = 0f; return v; }

    private void FaceDirection(Vector3 dir)
    {
        if (dir.sqrMagnitude < 1e-6f) return;
        Quaternion lookRot = Quaternion.LookRotation(dir.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, turnSpeed * Time.deltaTime);
    }

    // Moves at 'speed' and feeds the Animator the real velocity (walk/run without sliding)
    // + MotionSpeed=1.
    private void MoveAt(Vector3 dir, float speed)
    {
        float s = speed * SpeedMul;
        MoveStep(dir, s);
        SetAnimFloat(SpeedHash, s);
        SetAnimFloat(MotionSpeedHash, 1f);
    }

    private void MoveStep(Vector3 horizontalDir, float speed)
    {
        if (_controller != null && _controller.enabled)
        {
            if (_controller.isGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f;
            _verticalVelocity += gravity * Time.deltaTime;

            Vector3 motion = horizontalDir * speed + Vector3.up * _verticalVelocity;
            _controller.Move(motion * Time.deltaTime);
        }
        else
        {
            transform.position += horizontalDir * speed * Time.deltaTime;
        }
    }

    // Raises/lowers the guard: shield (frontal block) + Animator bool. Idempotent.
    private void ApplyGuard(bool on)
    {
        if (on == _guardApplied) return;
        _guardApplied = on;
        if (_shield != null) _shield.SetDefending(on);
        SetAnimBool(IsDefendingHash, on);
    }

    private void HandleDeath()
    {
        _deathHandled = true;
        CancelCombo();
        ApplyGuard(false);
        SetAnimFloat(SpeedHash, 0f);
        SetAnimTrigger(DeadHash);
    }

    // The boss skin can change at runtime (EnemySkinMirror): the active Animator changes.
    // We re-fetch it only when it's "stale".
    private void RefreshAnimatorIfStale()
    {
        if (_animator != null && _animator.gameObject.activeInHierarchy) return;
        _animator = GetComponentInChildren<Animator>();
        _animParams.Refresh(_animator);
    }

    private void SetAnimFloat(int hash, float value)
    {
        RefreshAnimatorIfStale();
        if (_animator != null && _animParams.Has(hash)) _animator.SetFloat(hash, value);
    }

    private void SetAnimBool(int hash, bool value)
    {
        RefreshAnimatorIfStale();
        if (_animator != null && _animParams.Has(hash)) _animator.SetBool(hash, value);
    }

    private void SetAnimTrigger(int hash)
    {
        RefreshAnimatorIfStale();
        if (_animator != null && _animParams.Has(hash)) _animator.SetTrigger(hash);
    }
}
