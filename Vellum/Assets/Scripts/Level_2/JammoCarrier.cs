using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Controls Jammo in the Act_02 arena: walks to a point and carries a piece that floats beside
/// him. Walk pattern reused from JammoGuideController (MoveTowards XZ + Slerp + Animator), with a
/// NavMeshAgent to avoid obstacles. Vulnerable: implements IDamageReaction → if hit WHILE carrying
/// a piece, it flags the drop (the piece is lost). On death (Health.Died) it stops movement and
/// coroutines; the death beat + level end live in JammoHealth.
/// </summary>
[RequireComponent(typeof(Health))]
public class JammoCarrier : MonoBehaviour, IDamageReaction
{
    [Header("Walking")]
    [Tooltip("NavMeshAgent run speed. Jammo_Anim.controller runs at Speed≈3.5 m/s: keep it near that value so the feet match the movement (no sliding).")]
    [SerializeField] private float runSpeed = 3.5f;
    [SerializeField] private float turnSpeed = 10f;
    [Tooltip("Degrees: above this angle difference Jammo turns in place (in idle, no sliding) before moving; below the threshold he goes straight, so short stretches don't micro-stop.")]
    [SerializeField] private float turnInPlaceThreshold = 30f;
    [SerializeField] private float arriveDistance = 0.15f;
    [SerializeField] private Animator jammoAnimator;

    [Header("Locomotion animator")]
    [Tooltip("Locomotion blend-tree float of Jammo_Anim.controller (m/s thresholds: 0 idle, 1 walk, 3.5 run). Fed with the agent's real velocity.")]
    [SerializeField] private string locomotionSpeedParam = "Speed";

    [Header("Activation (get-up)")]
    [Tooltip("Animator bool that activates Jammo (standing pose / unlocks locomotion). Keep it on Start by default; a director can call Activate() with the right timing.")]
    [SerializeField] private string animatorActivatedBool = "IsActivated";
    [SerializeField] private bool activateOnStart = true;

    [Header("Piece carrying")]
    [Tooltip("Point (child of Jammo) above which the carried piece floats.")]
    [SerializeField] private Transform carryAnchor;
    [SerializeField] private float bobAmplitude = 0.15f;
    [SerializeField] private float bobFrequency = 2f;
    [Tooltip("Duration of lifting the piece from the ground up above the head.")]
    [SerializeField] private float liftDuration = 0.5f;
    [Tooltip("Duration of the piece vanishing on delivery (scale-down to 0).")]
    [SerializeField] private float placeDuration = 0.3f;

    [Header("Rest position")]
    [SerializeField] private Transform homePost;

    [Header("Director hook (optional)")]
    [SerializeField] private CinematicFallManager cinematicManager;

    private Coroutine _bob;
    private NavMeshAgent _agent;
    private Health _health;
    private readonly AnimatorParameterCache _animParams = new AnimatorParameterCache();
    private int _speedHash;
    private int _activatedHash;

    private bool _carrying;
    private bool _dropRequested;

    public Transform HomePost => homePost;

    /// <summary>True while carrying a piece (from pickup to release/drop).</summary>
    public bool IsCarrying => _carrying;
    /// <summary>Dead: the director stops driving it and the coroutines exit.</summary>
    public bool IsDead { get; private set; }
    /// <summary>Hit while carrying: the director must make the piece be lost.</summary>
    public bool DropRequested => _dropRequested;
    /// <summary>Clears the pending drop request.</summary>
    public void ClearDropRequest() => _dropRequested = false;

    void Awake()
    {
        _speedHash = Animator.StringToHash(locomotionSpeedParam);
        _activatedHash = Animator.StringToHash(animatorActivatedBool);
        _animParams.Refresh(jammoAnimator);

        _agent = GetComponent<NavMeshAgent>();
        if (_agent != null)
        {
            _agent.speed = runSpeed;
            _agent.stoppingDistance = arriveDistance;
            _agent.updateRotation = true;
        }

        _health = GetComponent<Health>();
        if (_health != null) _health.Died += OnDied;
    }

    void OnDestroy()
    {
        if (_health != null) _health.Died -= OnDied;
    }

    /// <summary>IDamageReaction: every hit taken. If carrying a piece, it gets lost.</summary>
    public void OnDamaged(DamageInfo info)
    {
        if (_carrying) _dropRequested = true;
    }

    private void OnDied()
    {
        IsDead = true;
        if (_bob != null) { StopCoroutine(_bob); _bob = null; }
        if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = true;
        SetLocomotion(0f);
    }

    // Speed = real velocity in m/s: the blend tree picks idle/walk/run synced with the
    // movement (no sliding).
    private void SetLocomotion(float speed)
    {
        if (jammoAnimator != null && _animParams.Has(_speedHash))
            jammoAnimator.SetFloat(_speedHash, speed);
    }

    void Start()
    {
        if (activateOnStart) Activate();
    }

    /// <summary>Puts Jammo into the active (standing) pose. A director can call this with its own timing instead of Start.</summary>
    public void Activate() => SetActivated(true);

    /// <summary>Returns Jammo to the inactive pose.</summary>
    public void Deactivate() => SetActivated(false);

    private void SetActivated(bool value)
    {
        if (jammoAnimator != null && _animParams.Has(_activatedHash))
            jammoAnimator.SetBool(_activatedHash, value);
    }

    /// <summary>Rotates in place toward the point (in idle, no sliding) until roughly aligned; returns immediately if already facing it.</summary>
    private IEnumerator TurnInPlace(Vector3 worldTarget)
    {
        Vector3 flat = worldTarget - transform.position;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.0001f) yield break;

        Quaternion targetRot = Quaternion.LookRotation(flat.normalized);
        if (Quaternion.Angle(transform.rotation, targetRot) <= turnInPlaceThreshold)
            yield break;

        float elapsed = 0f;
        while (Quaternion.Angle(transform.rotation, targetRot) > turnInPlaceThreshold && elapsed < 2f)
        {
            SetLocomotion(0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    /// <summary>Walks to the point. With a NavMeshAgent it avoids obstacles (pedestal/carved statue); without agent/NavMesh it falls back to MoveTowards XZ.</summary>
    public IEnumerator WalkTo(Vector3 worldPos)
    {
        if (IsDead) yield break;

        if (_agent != null && _agent.isOnNavMesh && _agent.SetDestination(worldPos))
        {
            // First turn toward the path (stationary, in idle), then move: avoids the
            // "walk while rotating" that looked unnatural.
            _agent.isStopped = true;
            _agent.updateRotation = false;
            while (_agent.pathPending) yield return null; // valid steeringTarget
            yield return TurnInPlace(_agent.steeringTarget);
            _agent.updateRotation = true;
            _agent.isStopped = false;

            // wait for the path computation, then for arrival. Anti-stall timer:
            // if the path is blocked/partial and Jammo isn't advancing, continue
            // anyway (no deadlock of the director's coroutine).
            float stuck = 0f;
            while (_agent.pathPending ||
                   _agent.remainingDistance > _agent.stoppingDistance)
            {
                if (IsDead || _dropRequested) break; // death or lost piece: abort the walk
                SetLocomotion(_agent.velocity.magnitude);
                stuck = _agent.velocity.sqrMagnitude > 0.01f ? 0f : stuck + Time.deltaTime;
                if (stuck > 2f)
                {
                    Debug.LogWarning("[JammoCarrier] Path blocked/unreachable: continuing.", this);
                    break;
                }
                yield return null;
            }

            _agent.isStopped = true;
            SetLocomotion(0f);
            yield break;
        }

        // Fallback: no agent or NavMesh not baked.
        Vector3 target = new Vector3(worldPos.x, transform.position.y, worldPos.z);

        yield return TurnInPlace(target); // turn in place before walking
        SetLocomotion(runSpeed);

        while (Vector3.Distance(transform.position, target) > arriveDistance)
        {
            if (IsDead || _dropRequested) break; // death or lost piece: abort the walk
            Vector3 dir = target - transform.position;
            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion rot = Quaternion.LookRotation(dir.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, rot, turnSpeed * Time.deltaTime);
            }
            transform.position = Vector3.MoveTowards(transform.position, target, runSpeed * Time.deltaTime);
            yield return null;
        }

        SetLocomotion(0f);
    }

    /// <summary>Lifts the piece from the ground up above Jammo's head, then bobs it until placed. Scale unchanged (1).</summary>
    public IEnumerator PickUpRoutine(Transform piece)
    {
        if (piece == null || IsDead) yield break;

        // From here Jammo "carries": a hit taken will lose the piece.
        _carrying = true;
        _dropRequested = false;

        Transform anchor = carryAnchor != null ? carryAnchor : transform;
        Vector3 startPos = piece.position;

        float t = 0f;
        while (t < liftDuration)
        {
            if (IsDead || _dropRequested) yield break; // hit during pickup: the director will do the drop
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / liftDuration);
            float e = 1f - Mathf.Pow(1f - k, 3f); // ease-out (like VortexEnterTransition)
            piece.position = Vector3.Lerp(startPos, anchor.position, e);
            yield return null;
        }

        piece.SetParent(anchor, true);
        piece.localPosition = Vector3.zero;

        if (_bob != null) StopCoroutine(_bob);
        _bob = StartCoroutine(BobRoutine(piece));
    }

    /// <summary>Delivery: the piece (scale-1 Jammo) is detached and vanishes (scale-down). The solid part appears via StatueRig.OnSlotFilled; nothing is moved/aligned here.</summary>
    public IEnumerator ReleasePiece(Transform piece)
    {
        _carrying = false;
        if (_bob != null) { StopCoroutine(_bob); _bob = null; }
        if (piece == null) yield break;

        piece.SetParent(null, true); // detach from Jammo: the pool can reuse it

        Vector3 startScale = piece.localScale;
        float t = 0f;
        while (t < placeDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / placeDuration);
            float e = k * k * (3f - 2f * k); // smoothstep ease-in-out
            piece.localScale = Vector3.Lerp(startScale, Vector3.zero, e);
            yield return null;
        }
        piece.localScale = Vector3.zero;
    }

    /// <summary>Delivery that KEEPS the piece visible (stacking on the altar): detaches it from Jammo and moves it to the slot, scale unchanged (1). Pooling is done by the caller at end of phase, not here.</summary>
    public IEnumerator PlacePiece(Transform piece, Vector3 target)
    {
        _carrying = false;
        if (_bob != null) { StopCoroutine(_bob); _bob = null; }
        if (piece == null) yield break;

        piece.SetParent(null, true);

        Vector3 startPos = piece.position;
        float t = 0f;
        while (t < placeDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / placeDuration);
            float e = k * k * (3f - 2f * k); // smoothstep
            piece.position = Vector3.Lerp(startPos, target, e);
            yield return null;
        }
        piece.position = target;
    }

    /// <summary>Piece LOST: Jammo was hit while carrying it. Detaches it and drops it to the ground, vanishing (the statue does NOT progress for this piece).</summary>
    public IEnumerator DropCarriedPiece(Transform piece)
    {
        _carrying = false;
        if (_bob != null) { StopCoroutine(_bob); _bob = null; }
        if (piece == null) yield break;

        piece.SetParent(null, true);

        Vector3 startPos = piece.position;
        Vector3 endPos = startPos + Vector3.down * 0.5f; // thud to the ground
        Vector3 startScale = piece.localScale;
        float t = 0f;
        while (t < placeDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / placeDuration);
            float e = k * k * (3f - 2f * k); // smoothstep
            piece.position = Vector3.Lerp(startPos, endPos, e);
            piece.localScale = Vector3.Lerp(startScale, Vector3.zero, e);
            yield return null;
        }
        piece.localScale = Vector3.zero;
    }

    private IEnumerator BobRoutine(Transform piece)
    {
        float t = 0f;
        Vector3 basePos = piece.localPosition;
        while (piece != null)
        {
            t += Time.deltaTime * bobFrequency;
            piece.localPosition = basePos + Vector3.up * (Mathf.Sin(t) * bobAmplitude);
            yield return null;
        }
    }

    void OnDisable()
    {
        if (_bob != null) { StopCoroutine(_bob); _bob = null; }
    }
}
