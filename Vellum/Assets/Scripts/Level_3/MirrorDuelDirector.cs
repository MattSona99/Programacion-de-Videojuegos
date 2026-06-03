using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// Orchestrates the final level "The Mirror of Water". Two IDENTICAL phases (Sun, then Moon),
/// each in two beats:
///   1) Collection: every 'hitsPerPiece' Player hits (BossShield) **unlock** one of the pieces
///      ALREADY PLACED on the enemy altar. Jammo carries unlocked pieces to the Player's altar
///      (one slot per piece).
///   2) Damage window: once all pieces are delivered, the enemy becomes hittable.
///      Sun phase → down to 'phase1DamageFloor' (50%); Moon phase → down to 0 (Win).
///
/// Moon-phase differences: the boss is empowered (BossDuelAI) and can intercept Jammo mid-carry —
/// if hit, the piece RETURNS to its spot on the enemy altar (and Jammo retries) and Jammo takes
/// damage. In Sun phase Jammo is safe (the boss doesn't go for him).
///
/// Defeat: death of the Player or of Jammo (Game Over driven by PlayerHealth/JammoHealth). Reuses
/// JammoCarrier like Act_02. The pieces are persistent scene props (5 distinct prefabs): no
/// pooling, they just get repositioned.
/// </summary>
public class MirrorDuelDirector : MonoBehaviour
{
    private enum DuelPhase { Sun, Flipping, Moon, Won, Lost }

    [Header("Actors")]
    [SerializeField] private JammoCarrier jammo;
    [SerializeField] private BossDuelAI boss;
    [SerializeField] private BossShield bossShield;
    [SerializeField] private Health bossHealth;
    [SerializeField] private Health playerHealth;
    [SerializeField] private MirrorFlipDirector flip;

    [Header("Pieces (pre-placed on the enemy altar)")]
    [Tooltip("The prefabs already in the scene on the enemy altar. Jammo carries them one by one to the Player's altar.")]
    [SerializeField] private Transform[] enemyPieces;
    [Tooltip("Destination slots on the Player's altar, one per piece (same index as enemyPieces).")]
    [SerializeField] private Transform[] playerAltarSlots;

    [Header("Jammo stand points (on the ground, on the NavMesh)")]
    [Tooltip("Where Jammo stops to take the pieces (in front of the enemy altar). If empty, uses the piece position: risks making him enter the pedestal.")]
    [SerializeField] private Transform enemyStandPoint;
    [Tooltip("Where Jammo stops to place the pieces (in front of the Player's altar). If empty, uses the slot.")]
    [SerializeField] private Transform playerStandPoint;

    [Header("HUD")]
    [Tooltip("HUD bars (HudReveal) revealed with a fade at the start of the duel.")]
    [SerializeField] private HudReveal[] hudReveals;

    [Header("Cinematics — cameras (Cinemachine 3)")]
    [Tooltip("High overview camera that frames the whole arena during the intro and the phase transition.")]
    [SerializeField] private CinemachineVirtualCameraBase overviewCamera;
    [Tooltip("The normal gameplay camera (follows the Player).")]
    [SerializeField] private CinemachineVirtualCameraBase gameplayCamera;
    [SerializeField] private int activePriority = 20;
    [SerializeField] private int idlePriority = 5;
    [Tooltip("How long the Player stays framed before the camera rises to the overview at scene start.")]
    [SerializeField] private float initialPauseOnPlayer = 1f;
    [Tooltip("Duration of the slow blend to/from the overview camera.")]
    [SerializeField] private float cinematicBlendDuration = 2f;

    [Header("Cinematics — dialogues (3rd-person, past-tense narrator)")]
    [Tooltip("Intro narration: the duo finding themselves in the mirrored arena. Played before the Sun phase.")]
    [SerializeField] private DialogueAsset introDialogue;
    [Tooltip("Transition narration: the endless duel and the sun↔moon swap. Played at the Sun→Moon flip.")]
    [SerializeField] private DialogueAsset transitionDialogue;

    [Header("Cinematics — epilogue (Win)")]
    [Tooltip("Epilogue narration played after the reflection dies, while the mirror world dissolves.")]
    [SerializeField] private DialogueAsset epilogueDialogue;
    [Tooltip("Duration of the world-dissolve that crumbles the mirror arena around the defeated boss.")]
    [SerializeField] private float dissolveDuration = 3f;
    [Tooltip("Final radius of the global dissolve sphere (must cover the whole arena from the boss).")]
    [SerializeField] private float maxDissolveRadius = 60f;
    [Tooltip("Pause after the epilogue narration before the victory end screen is shown (onWin).")]
    [SerializeField] private float pauseBeforeEndScreen = 0.5f;

    [Header("Cinematics — spawn return (animated walk-back)")]
    [Tooltip("Player root (for the locked walk-back: drives its CharacterController + Animator). If empty, derived from playerHealth.")]
    [SerializeField] private Transform player;
    [SerializeField] private Transform playerSpawn;
    [SerializeField] private Transform bossSpawn;
    [SerializeField] private Transform jammoSpawn;
    [Tooltip("Player RUN-back speed during the transition (match the Boss runSpeed/blend tree so it plays the run anim without sliding).")]
    [SerializeField] private float playerRunBackSpeed = 4.5f;
    [Tooltip("Wait after hiding the HUD before the camera rises, so the exit animation has time to finish.")]
    [SerializeField] private float hudHideWait = 0.4f;
    [Tooltip("Sun/Moon object (or a marker in its direction): after walking back to spawn, the three actors turn to face it before the sky flips.")]
    [SerializeField] private Transform celestialLookTarget;
    [Tooltip("Seconds for the actors to rotate toward the Sun/Moon at the phase transition.")]
    [SerializeField] private float actorTurnDuration = 0.8f;
    [Tooltip("Adds a 180° offset to the turn so meshes whose front is on -Z face the target. Untick if the actors face away.")]
    [SerializeField] private bool flipActorFacing = true;

    [Header("Rules")]
    [Tooltip("Normalized health below which the enemy does NOT drop in Sun phase (0.5 = 50%). Moon phase → 0.")]
    [Range(0f, 1f)]
    [SerializeField] private float phase1DamageFloor = 0.5f;
    [Tooltip("Pieces Jammo delivers in the Sun phase. The Moon phase delivers the remaining ones (they stay on the Player altar between phases). E.g. 2 → Sun delivers 2, Moon delivers the other 3.")]
    [SerializeField] private int phase1PieceCount = 2;
    [SerializeField] private float pauseAfterPlace = 0.4f;

    [Header("Events")]
    [SerializeField] private UnityEvent onEnterSun;
    [SerializeField] private UnityEvent onEnterMoon;
    [SerializeField] private UnityEvent onWin;
    [SerializeField] private UnityEvent onLose;
    [Tooltip("0..1: pieces delivered / total (collection bar). The Boss health bar hooks bossHealth.onDamaged.")]
    [SerializeField] private HealthChangedEvent onCollectProgress;

    // Code-friendly events for the scoring binder (mirror the private UnityEvents above).
    /// <summary>Fired when the Moon phase begins (scoring: reached Moon).</summary>
    public event Action EnteredMoon;
    /// <summary>Fired when the duel is won, boss defeated (scoring).</summary>
    public event Action Won;
    /// <summary>Fired when the duel is lost, Player/Jammo died (scoring).</summary>
    public event Action Lost;
    /// <summary>Fired each time a piece is delivered to the Player's altar (scoring).</summary>
    public event Action PieceDelivered;
    /// <summary>Fired when a piece is lost in the Moon phase, Jammo hit while carrying (scoring).</summary>
    public event Action PieceLost;

    private DuelPhase _phase;
    private Transform[] _homeParent;   // pieces' initial parent/pose (enemy altar)
    private Vector3[] _homePos;
    private Vector3[] _homeScale;
    private readonly Queue<int> _releaseQueue = new Queue<int>(); // unlocked pieces, waiting for Jammo
    private int _releasedTotal;
    private int _delivered;
    private int _pendingRelease;
    private int _phaseTarget;   // cumulative delivered count to reach in the current phase (Sun=phase1, Moon=all)
    private bool _atHome;

    private Coroutine _loop;
    private WaitForSeconds _waitPause;

    private CinemachineBrain _brain;
    private CharacterController _playerController;
    private Animator _playerAnimator;
    private PlayerInput _playerInput;            // disabled directly during the locked cinematics
    private MonoBehaviour[] _playerLockScripts;  // ThirdPersonController / StarterAssetsInputs / PlayerCombat
    private static readonly int PlayerSpeedHash = Animator.StringToHash("Speed");
    private static readonly int PlayerMotionSpeedHash = Animator.StringToHash("MotionSpeed");

    // Global shader properties for the world-dissolve effect (shared with CinematicFallManager).
    private static readonly int GlobalPlayerPosId = Shader.PropertyToID("_GlobalPlayerPos");
    private static readonly int GlobalDissolveRadiusId = Shader.PropertyToID("_GlobalDissolveRadius");

    private int PieceCount => enemyPieces != null ? enemyPieces.Length : 0;

    void Awake()
    {
        _waitPause = new WaitForSeconds(pauseAfterPlace);

        // Cinematic refs: Brain for blend control, Player components for the locked walk-back.
        if (Camera.main != null) _brain = Camera.main.GetComponent<CinemachineBrain>();
        if (player == null && playerHealth != null) player = playerHealth.transform;
        if (player != null)
        {
            _playerController = player.GetComponent<CharacterController>();
            _playerAnimator = player.GetComponentInChildren<Animator>();
            _playerInput = player.GetComponentInChildren<PlayerInput>();

            // Cache the movement/combat scripts to disable directly during the locked cinematics
            // (same names DialogueManager matches). This makes the lock reliable even if the
            // DialogueManager's own player reference isn't wired in this scene.
            var lockList = new List<MonoBehaviour>();
            foreach (MonoBehaviour s in player.GetComponentsInChildren<MonoBehaviour>())
            {
                string scriptName = s.GetType().Name;
                if (scriptName == "ThirdPersonController" || scriptName == "StarterAssetsInputs" || scriptName == "PlayerCombat")
                    lockList.Add(s);
            }
            _playerLockScripts = lockList.ToArray();
        }

        int n = PieceCount;
        _homeParent = new Transform[n];
        _homePos = new Vector3[n];
        _homeScale = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            if (enemyPieces[i] == null) continue;
            _homeParent[i] = enemyPieces[i].parent;
            _homePos[i] = enemyPieces[i].position;
            _homeScale[i] = enemyPieces[i].localScale;
        }
    }

    // Start (not OnEnable): RevealHud() reveals HudReveal bars whose hiddenOnStart hides them in
    // Awake. Start is guaranteed after ALL Awakes, so the bars are initialized when we reveal them
    // (in OnEnable the reveal could run before their Awake and then be overwritten).
    void Start()
    {
        if (jammo == null || boss == null || bossShield == null || bossHealth == null ||
            flip == null || PieceCount == 0 ||
            playerAltarSlots == null || playerAltarSlots.Length < PieceCount)
        {
            Debug.LogWarning("[MirrorDuelDirector] Missing references or insufficient slots: the duel won't start.", this);
            return;
        }
        _loop = StartCoroutine(RunDuel());
    }

    void OnDisable()
    {
        if (_loop != null) { StopCoroutine(_loop); _loop = null; }
    }

    /// <summary>Called by BossShield every 'hitsPerPiece' Player hits (collection only): unlocks the next piece to carry. Capped to the current phase target (Sun=phase1PieceCount, Moon=all).</summary>
    public void NotifyPlayerBrokePiece()
    {
        if (_releasedTotal + _pendingRelease < _phaseTarget) _pendingRelease++;
    }

    private IEnumerator RunDuel()
    {
        flip.ApplyImmediate(false);

        yield return RunPrologue();

        yield return PhaseRoutine(moon: false);
        if (_phase == DuelPhase.Lost) { _loop = null; yield break; }

        yield return PhaseRoutine(moon: true);
        _loop = null;
    }

    /// <summary>
    /// Prologue: freeze the actors (Player locked, boss paused; Jammo idles by itself), rise to the
    /// overview camera, play the intro narration, then return to the Player, reveal the HUD and let
    /// the fight begin. Mirrors <see cref="Act02Director"/>'s prologue.
    /// </summary>
    private IEnumerator RunPrologue()
    {
        SetActiveCamera(gameplayCamera);
        LockPlayer(true);
        boss.SetPaused(true);

        if (initialPauseOnPlayer > 0f) yield return new WaitForSeconds(initialPauseOnPlayer);

        yield return BlendToCamera(overviewCamera, cinematicBlendDuration);
        yield return PlayDialogueAndWait(introDialogue);

        // PlayDialogue unlocks the Player when it ends: keep it locked through the blend back.
        LockPlayer(true);
        yield return BlendToCamera(gameplayCamera, cinematicBlendDuration);

        RevealHud();
        LockPlayer(false);
        boss.SetPaused(false);
    }

    private IEnumerator PhaseRoutine(bool moon)
    {
        _phase = moon ? DuelPhase.Moon : DuelPhase.Sun;
        boss.SetMoonPhase(moon);

        // Collection: the boss is NOT invulnerable, but takes 1 HP/hit (while Jammo
        // carries) and doesn't drop below the phase floor (Phase 1 = 50%).
        bossShield.SetInvulnerable(false);   // clear any leftover blockAll from the flip
        bossShield.SetShedding(true);
        bossHealth.SetMaxDamagePerHit(1f);
        bossHealth.SetDamageFloor(moon ? 0f : phase1DamageFloor * bossHealth.MaxHealth);

        // Cumulative target: Sun delivers phase1PieceCount; Moon delivers the rest (up to all).
        _phaseTarget = moon ? PieceCount : Mathf.Clamp(phase1PieceCount, 1, PieceCount);

        // Reset the pieces to the enemy altar ONLY at the start of the duel (Sun). In the Moon
        // phase the already-delivered pieces stay on the Player altar — Jammo only carries the rest.
        if (!moon)
        {
            ResetPiecesHome();
            _releasedTotal = 0;
            _delivered = 0;
        }
        _releaseQueue.Clear();
        _pendingRelease = 0;
        _atHome = false;
        ReportCollect();
        if (moon) { onEnterMoon.Invoke(); EnteredMoon?.Invoke(); } else onEnterSun.Invoke();

        // --- Collection: the Player unlocks pieces by hitting; Jammo carries them ---
        while (_delivered < _phaseTarget)
        {
            if (PlayerOrJammoDown()) { Lose(); yield break; }

            ProcessPendingReleases();

            if (_releaseQueue.Count > 0) yield return CarryOnePiece(_releaseQueue.Dequeue(), moon);
            else yield return GoHomeOnce(); // wait for the Player's hits
        }

        // --- Damage window: full damage (no more cap to 1) ---
        bossShield.SetShedding(false);
        bossHealth.SetMaxDamagePerHit(0f); // 0 = no cap; the phase floor remains

        float floor = moon ? 0f : phase1DamageFloor;
        while (!bossHealth.IsDead && bossHealth.Normalized > floor)
        {
            if (PlayerOrJammoDown()) { Lose(); yield break; }
            yield return null;
        }

        if (moon) { yield return RunEpilogue(); yield break; }

        // Sun phase complete → transition cinematic to the Moon phase. The delivered pieces STAY on
        // the Player altar (no reset); the Moon phase only collects the remaining ones.
        yield return RunPhaseTransition();
    }

    /// <summary>
    /// Phase transition (Sun → Moon): lock the Player and freeze the boss, hide the HUD (same as
    /// Esc) and rise to the overview camera, walk the three actors back to their spawn, play the
    /// sun↔moon flip, narrate the transition, then return to the Player, reveal the HUD and resume.
    /// </summary>
    private IEnumerator RunPhaseTransition()
    {
        bossShield.SetInvulnerable(true);
        _phase = DuelPhase.Flipping;
        LockPlayer(true);
        boss.SetPaused(true);

        HideHud();
        if (hudHideWait > 0f) yield return new WaitForSeconds(hudHideWait);
        yield return BlendToCamera(overviewCamera, cinematicBlendDuration);

        yield return WalkActorsToSpawn();

        // Beat: the three actors turn to face the Sun/Moon, then watch the sky flip.
        yield return TurnActorsToFace(celestialLookTarget, actorTurnDuration);

        // Sun ↔ Moon swap (sky/lights/layers). FlipTo locks/unlocks the Player itself.
        yield return flip.FlipTo(true);

        yield return PlayDialogueAndWait(transitionDialogue);

        // FlipTo / PlayDialogue unlock the Player when they end: keep it locked through the blend back.
        LockPlayer(true);
        yield return BlendToCamera(gameplayCamera, cinematicBlendDuration);

        RevealHud();
        LockPlayer(false);
        boss.SetPaused(false);
    }

    private void ProcessPendingReleases()
    {
        while (_pendingRelease > 0 && _releasedTotal < _phaseTarget)
        {
            _pendingRelease--;
            _releaseQueue.Enqueue(_releasedTotal);
            _releasedTotal++;
        }
    }

    private IEnumerator CarryOnePiece(int index, bool moon)
    {
        _atHome = false;
        Transform piece = enemyPieces[index];
        if (piece == null) { _delivered++; ReportCollect(); yield break; }

        // Jammo stops on the ground in front of the altar (not on top of the piece, which is at
        // the top of the pedestal): the piece then flies up to him in PickUpRoutine.
        Vector3 pickupStand = enemyStandPoint != null ? enemyStandPoint.position : piece.position;
        Vector3 dropStand = playerStandPoint != null ? playerStandPoint.position : playerAltarSlots[index].position;

        yield return jammo.WalkTo(pickupStand);
        yield return jammo.PickUpRoutine(piece);
        yield return jammo.WalkTo(dropStand);

        if (jammo.IsDead) yield break;

        // Moon phase only: hit while carrying → piece lost, returns to its spot.
        if (moon && jammo.DropRequested)
        {
            yield return jammo.DropCarriedPiece(piece);
            jammo.ClearDropRequest();
            ReturnPieceHome(index);
            PieceLost?.Invoke(); // scoring
            _releaseQueue.Enqueue(index); // already unlocked: Jammo retries, no extra hit
            yield return _waitPause;
            yield break;
        }

        yield return jammo.PlacePiece(piece, playerAltarSlots[index].position);
        _delivered++;
        PieceDelivered?.Invoke(); // scoring
        ReportCollect();
        yield return _waitPause;
    }

    /// <summary>Returns a piece to its initial pose on the enemy altar (parent/pos/scale).</summary>
    private void ReturnPieceHome(int index)
    {
        Transform piece = enemyPieces[index];
        if (piece == null) return;
        piece.SetParent(_homeParent[index], true);
        piece.position = _homePos[index];
        piece.localScale = _homeScale[index];
    }

    private void ResetPiecesHome()
    {
        for (int i = 0; i < PieceCount; i++) ReturnPieceHome(i);
    }

    private void RevealHud()
    {
        if (hudReveals == null) return;
        for (int i = 0; i < hudReveals.Length; i++)
            if (hudReveals[i] != null) hudReveals[i].Reveal();
    }

    /// <summary>Hides the HUD bars with the same slide+fade used by the pause menu (Esc).</summary>
    private void HideHud()
    {
        if (hudReveals == null) return;
        for (int i = 0; i < hudReveals.Length; i++)
            if (hudReveals[i] != null) hudReveals[i].Hide();
    }

    // ---- Cinematic helpers (camera / dialogue / walk-back) ----------------

    private void SetActiveCamera(CinemachineVirtualCameraBase target)
    {
        if (target != null) target.Priority = activePriority;
        if (overviewCamera != null && overviewCamera != target) overviewCamera.Priority = idlePriority;
        if (gameplayCamera != null && gameplayCamera != target) gameplayCamera.Priority = idlePriority;
    }

    /// <summary>Switches to <paramref name="cam"/> with a slow blend (overriding the Brain's DefaultBlend for the duration), then waits for the blend to finish. Same pattern as <see cref="Act02Director"/>.</summary>
    private IEnumerator BlendToCamera(CinemachineVirtualCameraBase cam, float duration)
    {
        CinemachineBlendDefinition prevBlend = default;
        bool overridden = false;
        if (_brain != null && duration > 0f)
        {
            prevBlend = _brain.DefaultBlend;
            _brain.DefaultBlend = new CinemachineBlendDefinition(prevBlend.Style, duration);
            overridden = true;
        }

        SetActiveCamera(cam);
        yield return WaitForBlend();

        if (overridden && _brain != null) _brain.DefaultBlend = prevBlend;
    }

    private IEnumerator WaitForBlend()
    {
        if (_brain == null) yield break;
        yield return new WaitWhile(() => _brain.IsBlending);
    }

    /// <summary>Plays a dialogue to completion (locking the Player for its duration via the DialogueManager) and waits for it. No-op with a warning if the asset or singleton are missing.</summary>
    private IEnumerator PlayDialogueAndWait(DialogueAsset dialogue)
    {
        if (dialogue == null || DialogueManager.Instance == null)
        {
            Debug.LogWarning("[MirrorDuelDirector] Dialogue or DialogueManager.Instance missing: skipping it.", this);
            yield break;
        }
        bool done = false;
        DialogueManager.Instance.PlayDialogue(dialogue, () => done = true);
        yield return new WaitUntil(() => done);
    }

    /// <summary>
    /// Locks/unlocks the Player around the cinematics. Routes through the <see cref="DialogueManager"/>
    /// when available AND toggles the cached player components directly with our own reference, so the
    /// lock holds from the very first frame even if the DialogueManager's player ref isn't wired here.
    /// </summary>
    private void LockPlayer(bool locked)
    {
        if (DialogueManager.Instance != null)
        {
            if (locked) DialogueManager.Instance.LockPlayer();
            else DialogueManager.Instance.UnlockPlayer();
        }

        if (_playerInput != null) _playerInput.enabled = !locked;
        if (_playerLockScripts != null)
            for (int i = 0; i < _playerLockScripts.Length; i++)
                if (_playerLockScripts[i] != null) _playerLockScripts[i].enabled = !locked;

        if (locked) SetPlayerLocomotion(0f); // freeze the locomotion blend tree immediately
    }

    /// <summary>Walks the three actors back to their spawn in parallel (Jammo via NavMesh, Boss and Player animated) and waits until all have arrived.</summary>
    private IEnumerator WalkActorsToSpawn()
    {
        int running = 0;
        if (boss != null && bossSpawn != null) { running++; StartCoroutine(RunChild(boss.ReturnToSpawn(bossSpawn.position), () => running--)); }
        if (jammo != null && jammoSpawn != null) { running++; StartCoroutine(RunChild(jammo.WalkTo(jammoSpawn.position), () => running--)); }
        if (player != null && playerSpawn != null) { running++; StartCoroutine(RunChild(PlayerWalkTo(playerSpawn.position), () => running--)); }

        yield return new WaitUntil(() => running == 0);
    }

    /// <summary>
    /// Smoothly rotates the Player, Boss and Jammo to face <paramref name="target"/> on the horizontal
    /// plane (a beat where they all look toward the Sun/Moon before the sky flips). No-op if the target
    /// is unassigned.
    /// </summary>
    private IEnumerator TurnActorsToFace(Transform target, float duration)
    {
        if (target == null) yield break;

        Transform[] actors = { player, boss != null ? boss.transform : null, jammo != null ? jammo.transform : null };
        Quaternion[] from = new Quaternion[actors.Length];
        Quaternion[] to = new Quaternion[actors.Length];
        for (int i = 0; i < actors.Length; i++)
        {
            if (actors[i] == null) continue;
            from[i] = actors[i].rotation;
            Vector3 dir = target.position - actors[i].position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion look = Quaternion.LookRotation(dir);
                if (flipActorFacing) look *= Quaternion.Euler(0f, 180f, 0f); // -Z-front meshes face the target
                to[i] = look;
            }
            else to[i] = actors[i].rotation;
        }

        float t = 0f;
        float dur = Mathf.Max(0.0001f, duration);
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / dur);
            for (int i = 0; i < actors.Length; i++)
                if (actors[i] != null) actors[i].rotation = Quaternion.Slerp(from[i], to[i], k);
            yield return null;
        }
        for (int i = 0; i < actors.Length; i++)
            if (actors[i] != null) actors[i].rotation = to[i];
    }

    /// <summary>Runs a child routine to completion, then signals <paramref name="onDone"/> (used to await several parallel walk-backs).</summary>
    private IEnumerator RunChild(IEnumerator routine, Action onDone)
    {
        yield return routine;
        onDone?.Invoke();
    }

    /// <summary>Walks the (locked) Player to <paramref name="target"/> via its CharacterController, feeding the locomotion Animator so it walks without sliding.</summary>
    private IEnumerator PlayerWalkTo(Vector3 target)
    {
        const float arriveDist = 0.2f;
        float timeout = 6f; // anti-stall safety

        while (timeout > 0f)
        {
            Vector3 to = target - player.position;
            to.y = 0f;
            float dist = to.magnitude;
            if (dist <= arriveDist) break;

            Vector3 dir = to / Mathf.Max(dist, 0.001f);
            player.rotation = Quaternion.Slerp(player.rotation, Quaternion.LookRotation(dir), 10f * Time.deltaTime);

            // The lock leaves the CharacterController enabled (only the input/controller scripts are off):
            // drive it directly, adding gravity so it stays grounded.
            if (_playerController != null && _playerController.enabled)
                _playerController.Move((dir * playerRunBackSpeed + Vector3.up * -9.81f) * Time.deltaTime);
            else
                player.position += dir * playerRunBackSpeed * Time.deltaTime;

            SetPlayerLocomotion(playerRunBackSpeed);
            timeout -= Time.deltaTime;
            yield return null;
        }

        SetPlayerLocomotion(0f);
    }

    private void SetPlayerLocomotion(float speed)
    {
        if (_playerAnimator == null) return;
        _playerAnimator.SetFloat(PlayerSpeedHash, speed);
        _playerAnimator.SetFloat(PlayerMotionSpeedHash, speed > 0.01f ? 1f : 0f);
    }

    private IEnumerator GoHomeOnce()
    {
        if (!_atHome && jammo.HomePost != null)
        {
            yield return jammo.WalkTo(jammo.HomePost.position);
            _atHome = true;
        }
        yield return null;
    }

    /// <summary>
    /// Epilogue (Moon-phase Win): freeze the actors (the boss auto-plays its "Dead" trigger), hide
    /// the HUD and rise to the overview camera, dissolve the whole mirror arena to grey, narrate the
    /// epilogue, then fire <see cref="onWin"/> (wired to the victory end screen). Mirrors the
    /// structure of <see cref="RunPhaseTransition"/>.
    /// </summary>
    private IEnumerator RunEpilogue()
    {
        _phase = DuelPhase.Won;
        LockPlayer(true);
        boss.SetPaused(true); // BossDuelAI.HandleDeath runs before the paused check, so "Dead" still plays.

        HideHud();
        if (hudHideWait > 0f) yield return new WaitForSeconds(hudHideWait);
        yield return BlendToCamera(overviewCamera, cinematicBlendDuration);

        yield return DissolveWorldAroundBoss();

        yield return PlayDialogueAndWait(epilogueDialogue);

        if (pauseBeforeEndScreen > 0f) yield return new WaitForSeconds(pauseBeforeEndScreen);

        // The victory end screen is driven by onWin (wired to MainMenuManager.ShowVictory). The
        // global dissolve radius stays high (the world is gone) and is reset on RestartGame.
        Won?.Invoke(); // scoring: record the win before the end screen reads the score
        onWin.Invoke();
    }

    /// <summary>
    /// Crumbles the mirror world by growing the GLOBAL dissolve sphere (same shader properties as
    /// <see cref="CinematicFallManager"/>) centred on the defeated boss, so the arena collapses to
    /// grey outward from the fallen reflection. Uses cached property IDs; no per-material references.
    /// </summary>
    private IEnumerator DissolveWorldAroundBoss()
    {
        Vector3 center = boss != null ? boss.transform.position : (player != null ? player.position : Vector3.zero);
        Shader.SetGlobalVector(GlobalPlayerPosId, center);

        float dur = Mathf.Max(0.0001f, dissolveDuration);
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            Shader.SetGlobalFloat(GlobalDissolveRadiusId, Mathf.Lerp(0f, maxDissolveRadius, t / dur));
            yield return null;
        }
        Shader.SetGlobalFloat(GlobalDissolveRadiusId, maxDissolveRadius);
    }

    private void Lose()
    {
        _phase = DuelPhase.Lost;
        Lost?.Invoke(); // scoring: record the loss (partial credit) before the game-over screen
        onLose.Invoke(); // the Game Over screen is driven by PlayerHealth/JammoHealth
    }

    private bool PlayerOrJammoDown()
        => (playerHealth != null && playerHealth.IsDead) || (jammo != null && jammo.IsDead);

    private void ReportCollect()
        => onCollectProgress.Invoke(PieceCount > 0 ? Mathf.Clamp01((float)_delivered / PieceCount) : 0f);
}
