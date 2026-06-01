using UnityEngine;

// AI nemico dell'arena Act_02 (movimento semplice, niente NavMesh).
// Bersaglio dinamico via EnemyTargetCoordinator: di default insegue Jammo;
// passa al Player se è stato colpito da lui e c'è uno slot Player libero
// (richieste #1/#2). Il COMPORTAMENTO verso il bersaglio (inseguire / attaccare
// / arretrare) è deciso da un controllore a Logica Difusa (EnemyFuzzyBrain).
// Gli attaccanti dello stesso bersaglio si dispongono a VENTAGLIO su slot
// angolari (coordinator) per non accodarsi; la separazione rifinisce.
// Implementa IDamageReaction per accorgersi di essere stato colpito dal Player.
// Richiede Health sullo stesso GameObject; KnockbackReceiver è opzionale.
[RequireComponent(typeof(Health))]
public class EnemyAI : MonoBehaviour, IDamageReaction
{
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int DeadHash = Animator.StringToHash("Dead");

    [Header("Movimento")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float turnSpeed = 10f;
    [SerializeField] private float gravity = -20f;

    [Header("Separazione (anti-overlap, movimento indipendente)")]
    [Tooltip("Layer dei nemici: usato per allontanarsi dai simili vicini e per contare gli alleati (fuzzy).")]
    [SerializeField] private LayerMask enemyMask;
    [SerializeField] private float separationRadius = 1.5f;
    [SerializeField] private float separationWeight = 1.5f;
    [Tooltip("Raggio entro cui contare gli alleati (input 'crowding' del fuzzy).")]
    [SerializeField] private float peerCheckRadius = 2.5f;

    [Header("Attacco / contatto")]
    [SerializeField] private float attackRange = 1.8f;
    [Tooltip("Distanza desiderata a cui il nemico SMETTE di avanzare e resta a ridosso del bersaglio. Viene comunque garantito un minimo di sicurezza dai raggi reali dei collider (vedi collisionMargin), così non sbatte mai contro il bersaglio. Continua a seguire il bersaglio se questo si allontana.")]
    [SerializeField] private float stopDistance = 1f;
    [Tooltip("Margine aggiunto alla somma dei raggi delle capsule per la distanza di stop minima di sicurezza (anti-jam contro il collider del bersaglio).")]
    [SerializeField] private float collisionMargin = 0.2f;
    [Tooltip("Cooldown con aggressività MINIMA (fuzzy aggression = 0).")]
    [SerializeField] private float maxAttackCooldown = 3f;
    [Tooltip("Cooldown con aggressività MASSIMA (fuzzy aggression = 1).")]
    [SerializeField] private float minAttackCooldown = 1f;
    [SerializeField] private float enemyDamage = 10f;

    [Header("Accerchiamento (effetto ventaglio)")]
    [Tooltip("Apertura angolare (gradi) tra uno slot e l'altro attorno al bersaglio.")]
    [SerializeField] private float fanSpacingDeg = 32f;

    [Header("Decisione fuzzy")]
    [Tooltip("Ogni quanti secondi rivalutare la decisione fuzzy (non ogni frame: niente alloc in hot path).")]
    [SerializeField] private float decisionInterval = 0.2f;

    [Header("Targeting")]
    [Tooltip("Ogni quanti secondi rivalutare il bersaglio (Player vs Jammo).")]
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
            Debug.LogWarning("[EnemyAI] Nessun CharacterController: il nemico non collide coi muri dell'arena.");
        else
            _ownRadius = _controller.radius;
        _knockback = GetComponent<KnockbackReceiver>();
        _animator = GetComponentInChildren<Animator>();
        _animParams.Refresh(_animator);
        _brain = new EnemyFuzzyBrain();
    }

    // Distanza di stop EFFETTIVA: mai sotto la somma dei raggi delle capsule
    // (nemico + bersaglio) + margine, così il nemico non sbatte contro il
    // collider del bersaglio (niente muro invisibile / spinta repulsiva).
    private float EffectiveStop() => Mathf.Max(stopDistance, _ownRadius + _targetRadius + collisionMargin);

    void OnDisable()
    {
        // Fuori dalle liste del coordinator (pooling): libera lo slot.
        if (EnemyTargetCoordinator.Instance != null) EnemyTargetCoordinator.Instance.Unregister(this);
    }

    // IDamageReaction: chiamato da Health a ogni danno subìto. Se il colpo viene
    // dal Player, il nemico viene "triggerato" e prova a ingaggiarlo subito.
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

        // Punto di standoff attorno al bersaglio (a stopDistance), su uno slot
        // angolare: con più attaccanti si aprono a ventaglio invece di accodarsi.
        Vector3 approach = ComputeApproachPoint(_currentTarget.position, dir);
        Vector3 toApproach = approach - transform.position;
        toApproach.y = 0f;
        Vector3 moveDir = toApproach.sqrMagnitude > 1e-4f ? toApproach.normalized : dir;

        // Attacca appena il bersaglio è a tiro, anche mentre fa l'ultimo passo.
        if (planarDist <= attackRange) { FaceDirection(dir); TryAttack(_currentTarget); }
        else { FaceDirection(moveDir); }

        // Gate sulla distanza DAL BERSAGLIO (non dal punto): smette di avanzare
        // appena è a ridosso, così non spinge contro il collider del bersaglio
        // (niente muro invisibile / forza repulsiva). Riprende se il bersaglio
        // si allontana oltre la distanza di stop effettiva.
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

    // Sceglie Player o Jammo secondo le regole del coordinator (richieste #1/#2).
    private void ResolveTarget()
    {
        var coord = EnemyTargetCoordinator.Instance;
        if (coord == null)
        {
            // Fallback (coordinator non in scena): punta il Player, così i nemici
            // non restano immobili durante un playtest prima del wiring.
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

        // Jammo morto = livello finito: nessun bersaglio, i nemici restano fermi.
        if (!coord.JammoAlive)
        {
            SetTarget(null, null);
            return;
        }

        bool playerAlive = coord.PlayerAlive;

        // Se sta già inseguendo il Player (slot riservato), continua finché vive.
        if (coord.IsChasingPlayer(this) && playerAlive)
        {
            SetTarget(coord.Player, coord.PlayerHealth);
            return;
        }

        // Triggerato dal Player e c'è uno slot libero → ingaggia il Player.
        if (_triggeredByPlayer && playerAlive && coord.TryClaimPlayer(this))
        {
            SetTarget(coord.Player, coord.PlayerHealth);
            return;
        }

        // Default: Jammo (vivo, garantito dal check sopra).
        SetTarget(coord.Jammo, coord.JammoHealth);
    }

    private void SetTarget(Transform t, Health h)
    {
        _currentTarget = t;
        _currentTargetHealth = h;

        // Raggio del collider del bersaglio (per la distanza di stop di sicurezza).
        // SetTarget gira su timer, non ogni frame → GetComponent ammesso.
        CharacterController tc = t != null ? t.GetComponentInParent<CharacterController>() : null;
        _targetRadius = tc != null ? tc.radius : 0f;

        var coord = EnemyTargetCoordinator.Instance;
        if (coord == null) return;

        if (t != null && t == coord.Jammo) coord.RegisterJammo(this);
        else if (t != null && t == coord.Player) { /* già in lista via TryClaimPlayer */ }
        else coord.Unregister(this);
    }

    // Punto di standoff a 'stopDistance' dal bersaglio, sul lato da cui il
    // nemico arriva. Con più attaccanti dello stesso bersaglio, ognuno ruota di
    // uno slot angolare → si aprono a ventaglio attorno al bersaglio (chi è
    // dietro punta a uno slot laterale libero e gira attorno).
    private Vector3 ComputeApproachPoint(Vector3 targetPos, Vector3 dir)
    {
        Vector3 fromTarget = -dir; // bersaglio → nemico

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
        // Aggressività fuzzy: alta → cooldown breve (attacca spesso), bassa → lungo.
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

    public void Configure() => ResetState();

    public void ResetState()
    {
        _deathHandled = false;
        _lastAttackTime = float.NegativeInfinity;
        _currentAttackCooldown = 0f;
        _verticalVelocity = 0f;

        // Targeting / fuzzy: riparte pulito (il GO viene riusato dal pool).
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
