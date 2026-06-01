using UnityEngine;
using UnityEngine.InputSystem;
using StarterAssets; 

public class PlayerCombat : MonoBehaviour
{
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int FinisherHash = Animator.StringToHash("Finisher");
    private static readonly int IsDefendingHash = Animator.StringToHash("IsDefending");

    [Tooltip("Se true, il Player può muoversi durante attacco/difesa (richiede un layer Animator upper-body con Avatar Mask, altrimenti i piedi scivolano).")]
    [SerializeField] private bool moveWhileActing = false;

    [Tooltip("Nome del layer Animator mascherato dei colpi leggeri. Il finisher lo porta a peso 0 perché è una clip full-body sul Base Layer.")]
    [SerializeField] private string upperBodyLayerName = "UpperBody";

    [Tooltip("Colpi mascherati prima del finisher: dopo questo numero di colpi, il click successivo lancia il finisher full-body.")]
    [SerializeField] private int comboHitsBeforeFinisher = 3;

    [Tooltip("Velocità (peso/sec) con cui il layer UpperBody torna a 0 a riposo. Il peso sale a 1 istantaneamente all'inizio di un'azione.")]
    [SerializeField] private float upperBodyBlendSpeed = 14f;

    private Animator _animator;
    private StarterAssetsInputs _inputs;
    private PlayerInput _playerInput;
    private PlayerMeleeAttack _meleeAttack;
    private readonly AnimatorParameterCache _animParams = new AnimatorParameterCache();

    // Caches the state of any locking action (Attack or Defend) to restore movement smoothly
    private bool _wasActionLocked = false;

    private bool _isDefending = false;
    public bool IsDefending => _isDefending;

    private int _upperBodyLayerIndex = -1;
    private int _comboStep = 0;
    private bool _finisherActive = false;

    void Start()
    {
        _inputs = GetComponent<StarterAssetsInputs>();
        _playerInput = GetComponent<PlayerInput>();
        _meleeAttack = GetComponent<PlayerMeleeAttack>();
        // NON chiamare RefreshAnimator qui: lo farà PlayerSkinSwitcher.Switch()
    }

    // Called by PlayerSkinSwitcher after toggling which Geometry_* is active,
    // so the cached _animator reference points to the active character's Animator.
    public void RefreshAnimator(GameObject activeGeometry = null)
    {
        if (activeGeometry != null)
            _animator = activeGeometry.GetComponent<Animator>();
        else
            _animator = GetComponentInChildren<Animator>();

        _animParams.Refresh(_animator);

        _upperBodyLayerIndex = _animator != null ? _animator.GetLayerIndex(upperBodyLayerName) : -1;

        // Reset al cambio skin: l'Animator nuovo riparte a riposo (peso 0: il Base
        // Layer guida tutto il corpo finché non parte un'azione).
        _comboStep = 0;
        _finisherActive = false;
        if (_animator != null && _upperBodyLayerIndex >= 0)
            _animator.SetLayerWeight(_upperBodyLayerIndex, 0f);
    }

    void Update()
    {
        // 1. COMBAT INPUT HANDLING 
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            if (Mouse.current != null)
            {
                bool defendHeld = Mouse.current.rightButton.isPressed;

                // Attack Logic (Left Click) — solo se NON si sta difendendo:
                // attacco e difesa sono mutuamente esclusivi. Il finisher è
                // committal: durante la sua esecuzione i click vengono ignorati.
                if (!defendHeld && Mouse.current.leftButton.wasPressedThisFrame
                    && _animator != null && !_finisherActive)
                {
                    bool canFinisher = _animParams.Has(FinisherHash)
                                       && _comboStep >= comboHitsBeforeFinisher;
                    if (canFinisher)
                    {
                        _animator.SetTrigger(FinisherHash);
                        if (_animParams.Has(AttackHash)) _animator.ResetTrigger(AttackHash);
                        OnFinisherStart();
                    }
                    else if (_animParams.Has(AttackHash))
                    {
                        _animator.SetTrigger(AttackHash);
                        _comboStep++;
                        // Snap del peso: il 1° frame del colpo non parte invisibile.
                        if (_upperBodyLayerIndex >= 0)
                            _animator.SetLayerWeight(_upperBodyLayerIndex, 1f);
                    }
                    if (_meleeAttack != null) _meleeAttack.BeginSwing();
                }

                // Defense Logic (Right Click Hold)
                _isDefending = defendHeld;
                if (_animator != null && _animParams.Has(IsDefendingHash))
                    _animator.SetBool(IsDefendingHash, _isDefending);

                // Alzando lo scudo annulla il colpo in corso e il trigger in coda,
                // così al rilascio della difesa non parte un attacco "fantasma".
                if (_isDefending)
                {
                    if (_meleeAttack != null) _meleeAttack.CancelSwing();
                    if (_animator != null && _animParams.Has(AttackHash)) _animator.ResetTrigger(AttackHash);
                    if (_animator != null && _animParams.Has(FinisherHash)) _animator.ResetTrigger(FinisherHash);
                    _comboStep = 0;
                }
            }
        }
        else
        {
            // Failsafe: Forces shield down if UI is opened
            _isDefending = false;
            if (_animator != null && _animParams.Has(IsDefendingHash))
                _animator.SetBool(IsDefendingHash, false);
        }

        // Peso dinamico del layer UpperBody + reset combo. A riposo il peso va a 0
        // (il Base Layer guida tutto il corpo: niente posa "congelata" sullo stato
        // None vuoto a Write Defaults OFF); sale a 1 mentre si attacca/difende; 0
        // durante il finisher (full-body sul Base Layer).
        if (_upperBodyLayerIndex >= 0 && _animator != null)
        {
            float current = _animator.GetLayerWeight(_upperBodyLayerIndex);
            if (_finisherActive)
            {
                _animator.SetLayerWeight(_upperBodyLayerIndex, 0f);
            }
            else
            {
                AnimatorStateInfo upperState = _animator.GetCurrentAnimatorStateInfo(_upperBodyLayerIndex);
                bool upperAttacking = upperState.IsTag("Attack");
                bool inTransition = _animator.IsInTransition(_upperBodyLayerIndex);
                bool acting = upperAttacking || upperState.IsTag("Defend") || inTransition;

                // Salita immediata (azione reattiva), discesa morbida (ritorno fluido a idle).
                float target = acting ? 1f : 0f;
                float next = target > current
                    ? target
                    : Mathf.MoveTowards(current, target, upperBodyBlendSpeed * Time.deltaTime);
                _animator.SetLayerWeight(_upperBodyLayerIndex, next);

                // Combo azzerata quando la catena mascherata è tornata a riposo.
                if (!upperAttacking && !inTransition)
                    _comboStep = 0;
            }
        }

        // 2. ROOT MOTION & MOVEMENT OVERRIDE HANDLING
        // Il finisher è full-body (gambe + root fanno la piroetta): blocca SEMPRE
        // il movimento, anche con moveWhileActing attivo. Con moveWhileActing OFF
        // si blocca invece su qualsiasi stato taggato Attack/Defend del Base Layer
        // (serve un layer upper-body mascherato, gambe in locomozione, busto in
        // azione, altrimenti i piedi scivolano).
        if (_animator != null && _inputs != null)
        {
            bool lockMovement = _finisherActive;

            if (!moveWhileActing && !lockMovement)
            {
                bool isAttacking = _animator.GetCurrentAnimatorStateInfo(0).IsTag("Attack");
                bool isDefendingState = _animator.GetCurrentAnimatorStateInfo(0).IsTag("Defend");
                lockMovement = isAttacking || isDefendingState;
            }

            if (lockMovement)
            {
                _inputs.move = Vector2.zero;
                _inputs.jump = false;

                _wasActionLocked = true;
            }
            // Once the action is complete (or canceled), restore directional input
            else if (_wasActionLocked)
            {
                if (_playerInput != null)
                {
                    _inputs.move = _playerInput.actions["Move"].ReadValue<Vector2>();
                }

                _wasActionLocked = false;
            }
        }
    }

    // Chiamati dalla SMB MeleeFinisherState sullo stato Attack04 del Base Layer
    // (e dal click che lancia il finisher, per azzerare subito il peso ed evitare
    // un frame di doppia posa). Idempotenti.
    public void OnFinisherStart()
    {
        _finisherActive = true;
        if (_animator != null && _upperBodyLayerIndex >= 0)
            _animator.SetLayerWeight(_upperBodyLayerIndex, 0f);
    }

    public void OnFinisherEnd()
    {
        // Il peso lo riporta a riposo (0) la gestione per-frame: a fine finisher si
        // è fermi, quindi non va forzato a 1.
        _finisherActive = false;
        _comboStep = 0;
    }
}