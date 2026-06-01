using UnityEngine;
using UnityEngine.InputSystem;
using StarterAssets; 

/// <summary>
/// Drives the Player's melee combat: a light-hit combo on a masked UpperBody Animator layer
/// that escalates into a full-body finisher, plus a hold-to-defend shield state. Attack and
/// defense are mutually exclusive; movement is locked during committal actions. Reads the new
/// Input System mouse and re-binds to the active mesh's Animator on skin switches.
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int FinisherHash = Animator.StringToHash("Finisher");
    private static readonly int IsDefendingHash = Animator.StringToHash("IsDefending");

    [Tooltip("If true, the Player can move during attack/defense (requires an upper-body Animator layer with an Avatar Mask, otherwise the feet slide).")]
    [SerializeField] private bool moveWhileActing = false;

    [Tooltip("Name of the masked Animator layer for light hits. The finisher drives it to weight 0 because it is a full-body clip on the Base Layer.")]
    [SerializeField] private string upperBodyLayerName = "UpperBody";

    [Tooltip("Masked hits before the finisher: after this many hits, the next click launches the full-body finisher.")]
    [SerializeField] private int comboHitsBeforeFinisher = 3;

    [Tooltip("Speed (weight/sec) at which the UpperBody layer returns to 0 at rest. The weight snaps to 1 instantly at the start of an action.")]
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
        // Do NOT call RefreshAnimator here: PlayerSkinSwitcher.Switch() will do it
    }

    /// <summary>
    /// Called by PlayerSkinSwitcher after toggling which Geometry_* is active, so the cached
    /// _animator reference points to the active character's Animator (and the layer index/combo reset).
    /// </summary>
    public void RefreshAnimator(GameObject activeGeometry = null)
    {
        if (activeGeometry != null)
            _animator = activeGeometry.GetComponent<Animator>();
        else
            _animator = GetComponentInChildren<Animator>();

        _animParams.Refresh(_animator);

        _upperBodyLayerIndex = _animator != null ? _animator.GetLayerIndex(upperBodyLayerName) : -1;

        // Reset on skin change: the new Animator starts at rest (weight 0: the Base
        // Layer drives the whole body until an action starts).
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

                // Attack Logic (Left Click) — only if NOT defending:
                // attack and defense are mutually exclusive. The finisher is
                // committal: clicks are ignored while it plays.
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
                        // Snap the weight: the hit's first frame doesn't start invisible.
                        if (_upperBodyLayerIndex >= 0)
                            _animator.SetLayerWeight(_upperBodyLayerIndex, 1f);
                    }
                    if (_meleeAttack != null) _meleeAttack.BeginSwing();
                }

                // Defense Logic (Right Click Hold)
                _isDefending = defendHeld;
                if (_animator != null && _animParams.Has(IsDefendingHash))
                    _animator.SetBool(IsDefendingHash, _isDefending);

                // Raising the shield cancels the in-progress hit and the queued trigger,
                // so releasing defense doesn't fire a "phantom" attack.
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

        // Dynamic UpperBody layer weight + combo reset. At rest the weight goes to 0
        // (the Base Layer drives the whole body: no pose "frozen" on the empty None state
        // with Write Defaults OFF); rises to 1 while attacking/defending; 0 during the
        // finisher (full-body on the Base Layer).
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

                // Instant rise (reactive action), smooth fall (fluid return to idle).
                float target = acting ? 1f : 0f;
                float next = target > current
                    ? target
                    : Mathf.MoveTowards(current, target, upperBodyBlendSpeed * Time.deltaTime);
                _animator.SetLayerWeight(_upperBodyLayerIndex, next);

                // Combo reset once the masked chain has returned to rest.
                if (!upperAttacking && !inTransition)
                    _comboStep = 0;
            }
        }

        // 2. ROOT MOTION & MOVEMENT OVERRIDE HANDLING
        // The finisher is full-body (legs + root do the spin): it ALWAYS locks movement,
        // even with moveWhileActing enabled. With moveWhileActing OFF, movement is instead
        // locked on any Attack/Defend-tagged state of the Base Layer (needs a masked
        // upper-body layer — legs in locomotion, torso acting — otherwise the feet slide).
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

    /// <summary>
    /// Called by the MeleeFinisherState SMB on the Base Layer's Attack04 state (and by the
    /// click that launches the finisher, to zero the weight immediately and avoid a
    /// double-pose frame). Idempotent.
    /// </summary>
    public void OnFinisherStart()
    {
        _finisherActive = true;
        if (_animator != null && _upperBodyLayerIndex >= 0)
            _animator.SetLayerWeight(_upperBodyLayerIndex, 0f);
    }

    /// <summary>Called by the MeleeFinisherState SMB on finisher exit. Clears the finisher flag and combo.</summary>
    public void OnFinisherEnd()
    {
        // The per-frame logic returns the weight to rest (0): the finisher ends at a
        // standstill, so it must not be forced to 1.
        _finisherActive = false;
        _comboStep = 0;
    }
}