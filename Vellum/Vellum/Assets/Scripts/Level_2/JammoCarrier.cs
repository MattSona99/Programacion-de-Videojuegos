using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// Controller del Jammo nell'arena Act_02: cammina verso un punto e trasporta
// un pezzo che fluttua accanto a lui. Sostituisce JammoGuideController (logica
// del livello 1) su questo Jammo. Pattern di cammino ripreso da
// JammoGuideController.FollowPathRoutine (MoveTowards XZ + Slerp + Animator).
// Vulnerabile: implementa IDamageReaction → se viene colpito MENTRE trasporta
// un pezzo, segnala il drop (il pezzo va perso). Alla morte (Health.Died) ferma
// movimento e coroutine; il "beat" di morte + fine livello sono in JammoHealth.
[RequireComponent(typeof(Health))]
public class JammoCarrier : MonoBehaviour, IDamageReaction
{
    [Header("Cammino")]
    [Tooltip("Velocità di corsa del NavMeshAgent. Jammo_Anim.controller usa la corsa a Speed≈3.5 m/s: tienila vicino a quel valore così i piedi combaciano col movimento (no slittamento).")]
    [SerializeField] private float runSpeed = 3.5f;
    [SerializeField] private float turnSpeed = 10f;
    [Tooltip("Gradi: sopra questa differenza d'angolo Jammo si gira sul posto (in idle, niente slittamento) prima di partire; sotto la soglia parte diretto, così i tratti brevi non micro-stoppano.")]
    [SerializeField] private float turnInPlaceThreshold = 30f;
    [SerializeField] private float arriveDistance = 0.15f;
    [SerializeField] private Animator jammoAnimator;

    [Header("Animator locomozione")]
    [Tooltip("Float del blend tree di locomozione di Jammo_Anim.controller (soglie m/s: 0 idle, 1 walk, 3.5 run). Viene alimentato con la velocità reale dell'agent.")]
    [SerializeField] private string locomotionSpeedParam = "Speed";

    [Header("Attivazione (alzata)")]
    [Tooltip("Bool dell'Animator che attiva Jammo (posa in piedi / sblocca la locomozione). In #5 va tenuto su Start; in #6 la regia chiamerà Activate() col timing giusto.")]
    [SerializeField] private string animatorActivatedBool = "IsActivated";
    [SerializeField] private bool activateOnStart = true;

    [Header("Trasporto pezzo")]
    [Tooltip("Punto (figlio di Jammo) sopra cui fluttua il pezzo trasportato.")]
    [SerializeField] private Transform carryAnchor;
    [SerializeField] private float bobAmplitude = 0.15f;
    [SerializeField] private float bobFrequency = 2f;
    [Tooltip("Durata del sollevamento da terra fino sopra la testa.")]
    [SerializeField] private float liftDuration = 0.5f;
    [Tooltip("Durata della sparizione del pezzo alla consegna (scale-down a 0).")]
    [SerializeField] private float placeDuration = 0.3f;

    [Header("Posizione di riposo")]
    [SerializeField] private Transform homePost;

    [Header("Hook #6 (opzionale, non usato in #5)")]
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

    // Sta trasportando un pezzo (dalla presa al rilascio/drop).
    public bool IsCarrying => _carrying;
    // Morto: il director smette di pilotarlo e le coroutine escono.
    public bool IsDead { get; private set; }
    // Colpito mentre trasportava: il director deve far perdere il pezzo.
    public bool DropRequested => _dropRequested;
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

    // IDamageReaction: ogni colpo subìto. Se sta trasportando un pezzo, lo perde.
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

    // Speed = velocità reale in m/s: il blend tree sceglie idle/walk/run
    // sincronizzato col movimento (niente slittamento).
    private void SetLocomotion(float speed)
    {
        if (jammoAnimator != null && _animParams.Has(_speedHash))
            jammoAnimator.SetFloat(_speedHash, speed);
    }

    void Start()
    {
        if (activateOnStart) Activate();
    }

    // Mette Jammo nella posa attiva (in piedi). #6 può chiamarlo col timing
    // della regia invece di Start (con activateOnStart = false).
    public void Activate() => SetActivated(true);

    public void Deactivate() => SetActivated(false);

    private void SetActivated(bool value)
    {
        if (jammoAnimator != null && _animParams.Has(_activatedHash))
            jammoAnimator.SetBool(_activatedHash, value);
    }

    // Ruota sul posto verso il punto, in idle (niente slittamento), finché non
    // è grossomodo allineato. Se è già rivolto là (sotto soglia) ritorna subito,
    // così i tratti brevi non micro-stoppano.
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

    // Cammina fino al punto. Con NavMeshAgent aggira gli ostacoli (piedistallo/
    // statua carved); senza agent/NavMesh ricade sul vecchio MoveTowards XZ.
    public IEnumerator WalkTo(Vector3 worldPos)
    {
        if (IsDead) yield break;

        if (_agent != null && _agent.isOnNavMesh && _agent.SetDestination(worldPos))
        {
            // Prima si gira verso il path (fermo, in idle), poi parte: evita il
            // "cammina mentre ruota" che sembrava innaturale.
            _agent.isStopped = true;
            _agent.updateRotation = false;
            while (_agent.pathPending) yield return null; // steeringTarget valido
            yield return TurnInPlace(_agent.steeringTarget);
            _agent.updateRotation = true;
            _agent.isStopped = false;

            // attende il calcolo del path, poi l'arrivo. Timer anti-stallo:
            // se il path è bloccato/parziale e Jammo non avanza, prosegue
            // comunque (no deadlock della coroutine del director).
            float stuck = 0f;
            while (_agent.pathPending ||
                   _agent.remainingDistance > _agent.stoppingDistance)
            {
                if (IsDead || _dropRequested) break; // morte o pezzo perso: interrompi il cammino
                SetLocomotion(_agent.velocity.magnitude);
                stuck = _agent.velocity.sqrMagnitude > 0.01f ? 0f : stuck + Time.deltaTime;
                if (stuck > 2f)
                {
                    Debug.LogWarning("[JammoCarrier] Path bloccato/irraggiungibile: proseguo.", this);
                    break;
                }
                yield return null;
            }

            _agent.isStopped = true;
            SetLocomotion(0f);
            yield break;
        }

        // Fallback: nessun agent o NavMesh non bakeata.
        Vector3 target = new Vector3(worldPos.x, transform.position.y, worldPos.z);

        yield return TurnInPlace(target); // gira fermo prima di camminare
        SetLocomotion(runSpeed);

        while (Vector3.Distance(transform.position, target) > arriveDistance)
        {
            if (IsDead || _dropRequested) break; // morte o pezzo perso: interrompi il cammino
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

    // Il pezzo si solleva da terra fino sopra la testa di Jammo, poi fluttua
    // (bob) finché non viene posizionato. Scala invariata (1).
    public IEnumerator PickUpRoutine(Transform piece)
    {
        if (piece == null || IsDead) yield break;

        // Da qui Jammo "trasporta": un colpo subìto farà perdere il pezzo.
        _carrying = true;
        _dropRequested = false;

        Transform anchor = carryAnchor != null ? carryAnchor : transform;
        Vector3 startPos = piece.position;

        float t = 0f;
        while (t < liftDuration)
        {
            if (IsDead || _dropRequested) yield break; // colpito durante la presa: il director farà il drop
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / liftDuration);
            float e = 1f - Mathf.Pow(1f - k, 3f); // ease-out (come VortexEnterTransition)
            piece.position = Vector3.Lerp(startPos, anchor.position, e);
            yield return null;
        }

        piece.SetParent(anchor, true);
        piece.localPosition = Vector3.zero;

        if (_bob != null) StopCoroutine(_bob);
        _bob = StartCoroutine(BobRoutine(piece));
    }

    // Consegna: il pezzo (Jammo scala-1) viene staccato da Jammo e svanisce
    // (scale-down). La parte solida la fa comparire StatueRig.OnSlotFilled sul
    // renderer della statua grande; qui non si sposta/allinea nulla.
    public IEnumerator ReleasePiece(Transform piece)
    {
        _carrying = false;
        if (_bob != null) { StopCoroutine(_bob); _bob = null; }
        if (piece == null) yield break;

        piece.SetParent(null, true); // stacca da Jammo: il pool può riusarlo

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

    // Consegna che LASCIA il pezzo visibile (impilamento sull'altare): lo stacca
    // da Jammo e lo porta allo slot, scala invariata (1). Il pooling lo fa il
    // chiamante a fine fase, non qui.
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

    // Pezzo PERSO: Jammo è stato colpito mentre lo trasportava. Lo stacca e lo
    // fa cadere a terra svanendo (la statua NON progredisce per questo pezzo).
    public IEnumerator DropCarriedPiece(Transform piece)
    {
        _carrying = false;
        if (_bob != null) { StopCoroutine(_bob); _bob = null; }
        if (piece == null) yield break;

        piece.SetParent(null, true);

        Vector3 startPos = piece.position;
        Vector3 endPos = startPos + Vector3.down * 0.5f; // tonfo a terra
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
