using System.Collections;
using UnityEngine;

// IA del doppelganger del livello finale. Separata da EnemyAI (l'arena a ondate):
// qui c'è un solo boss, niente coordinator/ventaglio. Locomozione via
// CharacterController (niente NavMesh).
//
// Ritmo di combattimento a finestre, per non essere monotono:
//   - AGGRO: insegue il bersaglio e, in range, esegue una COMBO (più colpi di
//     fila, come la combo del Player). Resta fermo durante la combo (no slide).
//   - REPOSITION: corre (run) verso un punto casuale per riposizionarsi.
//   - GUARD: arrivato, alza la guardia DA FERMO e aspetta il Player; poi decide
//     se attaccare o riposizionarsi di nuovo.
// Sopra a tutto: DIFESA REATTIVA — quando vede partire lo swing del Player
// (PlayerMeleeAttack.IsSwinging) ed è vicino e di fronte, con una certa
// probabilità alza la guardia per bloccare quel colpo (non sempre).
//
// Cammina E corre: l'Animator (clone del Player) ha un blend tree walk/run su
// 'Speed' (m/s reali) + 'MotionSpeed'. Avvicinamento = walk, riposizionamento = run.
//
// Fase 2 (Luna): potenziato; può STACCARSI dal Player per intercettare Jammo
// carico (BossFuzzyBrain). Il director mette in pausa il boss durante i flip e
// nella finestra del colpo finale (SetPaused). Niente KnockbackReceiver: il boss
// combatte piantato.
[RequireComponent(typeof(Health))]
public class BossDuelAI : MonoBehaviour
{
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int MotionSpeedHash = Animator.StringToHash("MotionSpeed");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int IsDefendingHash = Animator.StringToHash("IsDefending");
    private static readonly int DeadHash = Animator.StringToHash("Dead");

    private enum State { Aggro, Reposition, Guard, Kite, SeekHealth }

    [Header("Bersagli")]
    [SerializeField] private Transform player;
    [SerializeField] private JammoCarrier jammo;
    [Tooltip("PlayerMeleeAttack del Player (per la difesa reattiva). Se vuoto lo cerca tra i figli del Player.")]
    [SerializeField] private PlayerMeleeAttack playerMelee;

    [Header("Movimento (walk/run)")]
    [SerializeField] private float walkSpeed = 2f;
    [SerializeField] private float runSpeed = 4.5f;
    [Tooltip("Oltre questa distanza dal bersaglio corre (run) invece di camminare.")]
    [SerializeField] private float runDistance = 4f;
    [SerializeField] private float turnSpeed = 10f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float stopDistance = 1.6f;

    [Header("Attacco / combo")]
    [SerializeField] private float attackRange = 1.9f;
    [Tooltip("Numero di colpi della combo (= combo a 4 dell'Animator).")]
    [SerializeField] private int comboLength = 4;
    [Tooltip("Intervallo tra un colpo e l'altro della combo.")]
    [SerializeField] private float comboHitInterval = 0.28f;
    [Tooltip("Recupero dopo una combo prima di poterne fare un'altra.")]
    [SerializeField] private float comboRecovery = 1.2f;
    [SerializeField] private float attackDamage = 12f;

    [Header("Finestre attacco/difesa")]
    [Tooltip("Durata della finestra d'attacco prima di riposizionarsi.")]
    [SerializeField] private float aggroWindow = 5f;
    [Tooltip("Tempo massimo di attesa in guardia prima di ridecidere.")]
    [SerializeField] private float guardWindow = 3f;
    [Tooltip("Raggio attorno al Player entro cui scegliere il punto di riposizionamento.")]
    [SerializeField] private float repositionRadius = 6f;
    [SerializeField] private float repositionArriveDistance = 0.6f;
    [Tooltip("Distanza minima del punto di riposizionamento dalla posizione ATTUALE del boss: evita di 'arrivare' subito e ciclare Reposition→Guard→Reposition sul posto.")]
    [SerializeField] private float repositionMinDistance = 3.5f;
    [Tooltip("Tempo massimo per raggiungere il punto (anti-stallo).")]
    [SerializeField] private float repositionTimeout = 4f;
    [Tooltip("Distanza a cui, in guardia, il Player è 'arrivato' e il boss ridecide.")]
    [SerializeField] private float engageDistance = 3f;
    [Range(0f, 1f)]
    [Tooltip("Probabilità di passare all'attacco (anziché ri-riposizionarsi) quando esce dalla guardia.")]
    [SerializeField] private float attackAfterGuardChance = 0.7f;

    [Header("Kiting (richiede layer Animator upper-body, altrimenti scivola)")]
    [Range(0f, 1f)]
    [Tooltip("Probabilità di fare kiting (circolare a guardia alta) invece del riposizionamento fermo, a fine finestra d'attacco. Tieni 0 finché non hai il layer mascherato.")]
    [SerializeField] private float kiteChance = 0f;
    [SerializeField] private float kiteWindow = 4f;
    [Tooltip("Distanza dal Player che il boss cerca di mantenere mentre fa kiting.")]
    [SerializeField] private float kiteDistance = 4f;
    [SerializeField] private float kiteSpeed = 2.5f;
    [Tooltip("Ogni quanti secondi inverte il senso di rotazione attorno al Player.")]
    [SerializeField] private float kiteFlipInterval = 1.5f;

    [Header("Difesa reattiva")]
    [Range(0f, 1f)]
    [Tooltip("Probabilità di alzare la guardia quando vede partire lo swing del Player.")]
    [SerializeField] private float reactiveBlockChance = 0.45f;
    [SerializeField] private float reactiveBlockTime = 0.6f;
    [Tooltip("Distanza massima a cui reagisce allo swing del Player.")]
    [SerializeField] private float reactiveBlockRange = 2.6f;

    [Header("Potenziamento Fase 2 (Luna)")]
    [Tooltip("Moltiplicatore di walk/run quando è Fase Luna.")]
    [SerializeField] private float moonSpeedMultiplier = 1.3f;
    [SerializeField] private float moonAttackDamage = 18f;
    [SerializeField] private float moonComboRecovery = 0.7f;

    [Header("Decisione fuzzy (solo Fase 2)")]
    [SerializeField] private float decisionInterval = 0.2f;
    [Range(0f, 1f)]
    [SerializeField] private float interceptThreshold = 0.5f;
    [Tooltip("Oltre questa distanza il boss MOLLA l'intercetto di Jammo (isteresi: una volta agganciato insegue finché Jammo è carico e più vicino di così).")]
    [SerializeField] private float interceptGiveUpDistance = 12f;

    [Header("Recupero vita (pickup neutri)")]
    [Tooltip("Spawner dei pickup di vita del duello. Se vuoto, il boss non cerca mai vita.")]
    [SerializeField] private DuelHealthSpawner healthSpawner;
    [Range(0f, 1f)]
    [Tooltip("Soglia fuzzy sopra cui il boss si stacca per andare a curarsi.")]
    [SerializeField] private float seekHealthThreshold = 0.55f;
    [Tooltip("Ritardo prima che il boss 'noti' un pickup appena comparso (head start al Player).")]
    [SerializeField] private float pickupReactionDelay = 1.5f;
    [Tooltip("Velocità con cui corre verso il pickup.")]
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

    public void SetMoonPhase(bool moon) => _moonPhase = moon;

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

        // --- Difesa reattiva: rising edge dello swing del Player ---
        DetectReactiveBlock();

        bool reactiveActive = Time.time < _reactiveBlockUntil;

        // Durante una combo o un blocco reattivo il boss resta fermo (no slide):
        // solo gravità + facing verso il Player.
        if (_comboRoutine != null || reactiveActive)
        {
            ApplyGuard(reactiveActive); // la combo NON alza la guardia; il blocco reattivo sì
            if (player != null) FaceDirection(Planar(player.position - transform.position));
            SetAnimFloat(SpeedHash, 0f);
            MoveStep(Vector3.zero, 0f);
            return;
        }

        // Priorità massima: se ferito e c'è un pickup pronto e vicino, va a curarsi.
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

    // ---- Stati ------------------------------------------------------------

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
        MoveAt(dir, runSpeed); // riposizionamento = corsa
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
            // Niente due riposizionamenti di fila: se siamo arrivati qui da un
            // Reposition, si torna ad attaccare (evita il vagare passivo).
            if (_lastWasReposition || Random.value < attackAfterGuardChance) EnterState(State.Aggro);
            else EnterState(State.Reposition);
        }
    }

    // Kiting: circola attorno al Player a guardia alta mantenendo kiteDistance,
    // invertendo ogni tanto il senso. Richiede il layer Animator upper-body
    // (altrimenti scivola). Alla fine torna in attacco.
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
        float radial = Mathf.Clamp(dist - kiteDistance, -1f, 1f); // >0 troppo lontano (avvicina), <0 troppo vicino (allontana)
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
                _stateTimer = repositionTimeout; // safety anti-stallo
                break;
        }
    }

    private Vector3 PickRepositionPoint()
    {
        Vector3 around = player != null ? player.position : transform.position;
        Vector3 self = transform.position;
        Vector3 p = self;

        // Campiona un punto sul cerchio attorno al Player che disti almeno
        // repositionMinDistance dalla posizione attuale del boss (così si muove
        // davvero, niente arrivo istantaneo).
        for (int attempt = 0; attempt < 6; attempt++)
        {
            float angle = Random.value * Mathf.PI * 2f;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * repositionRadius;
            p = around + offset;
            p.y = self.y;
            if (Planar(p - self).magnitude >= repositionMinDistance) return p;
        }
        return p; // fallback: l'ultimo campionato
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
        if (Vector3.Angle(transform.forward, to) > 90f) return; // il Player deve essere davanti

        if (Random.value < reactiveBlockChance)
            _reactiveBlockUntil = Time.time + reactiveBlockTime;
    }

    // ---- Bersaglio (fuzzy in Fase 2) -------------------------------------

    private Transform ResolveTarget()
    {
        if (_moonPhase && jammo != null && !jammo.IsDead && jammo.IsCarrying)
        {
            float jammoDist = Planar(jammo.transform.position - transform.position).magnitude;

            _decisionTimer -= Time.deltaTime;
            if (_decisionTimer <= 0f)
            {
                _decisionTimer = decisionInterval;
                // Il fuzzy ATTIVA l'intercetto; a spegnerlo ci pensa l'isteresi sotto
                // (niente flip-flop al bordo soglia ogni 0.2 s).
                if (!_interceptJammo && _brain.Intercept(jammoDist, true) >= interceptThreshold)
                    _interceptJammo = true;
            }

            // Isteresi: una volta agganciato, insegue Jammo finché è carico e non
            // troppo lontano. Se si allontana oltre la soglia, molla.
            if (_interceptJammo && jammoDist <= interceptGiveUpDistance)
                return jammo.transform;
        }

        _interceptJammo = false;
        return player;
    }

    // ---- Recupero vita (decisione fuzzy gated su HP) ---------------------

    // Decide se staccarsi per andare a curarsi: solo se ferito e con un pickup
    // "pronto" (vivo da almeno pickupReactionDelay) abbastanza vicino. Priorità
    // massima: una volta in SeekHealth ci resta (isteresi) finché il pickup esiste.
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

        // Pickup raccolto/scaduto (disattivato dal pool) → torna a combattere.
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

        // La cura avviene da sé camminando sul pickup (trigger). Safety anti-stallo:
        _stateTimer -= Time.deltaTime;
        if (_stateTimer <= 0f) { _healthTarget = null; EnterState(State.Aggro); }
    }

    // ---- Locomozione / animazione ----------------------------------------

    private static Vector3 Planar(Vector3 v) { v.y = 0f; return v; }

    private void FaceDirection(Vector3 dir)
    {
        if (dir.sqrMagnitude < 1e-6f) return;
        Quaternion lookRot = Quaternion.LookRotation(dir.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, turnSpeed * Time.deltaTime);
    }

    // Muove a 'speed' e alimenta l'Animator con la velocità reale (walk/run senza
    // slittamento) + MotionSpeed=1.
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

    // Alza/abbassa la guardia: scudo (blocco frontale) + bool Animator. Idempotente.
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

    // La skin del boss può cambiare a runtime (EnemySkinMirror): l'Animator
    // attivo cambia. Lo ri-prendiamo solo quando è "stantio".
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
