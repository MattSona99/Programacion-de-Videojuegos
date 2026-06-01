using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

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

    [Header("Rules")]
    [Tooltip("Normalized health below which the enemy does NOT drop in Sun phase (0.5 = 50%). Moon phase → 0.")]
    [Range(0f, 1f)]
    [SerializeField] private float phase1DamageFloor = 0.5f;
    [SerializeField] private float pauseAfterPlace = 0.4f;

    [Header("Events")]
    [SerializeField] private UnityEvent onEnterSun;
    [SerializeField] private UnityEvent onEnterMoon;
    [SerializeField] private UnityEvent onWin;
    [SerializeField] private UnityEvent onLose;
    [Tooltip("0..1: pieces delivered / total (collection bar). The Boss health bar hooks bossHealth.onDamaged.")]
    [SerializeField] private HealthChangedEvent onCollectProgress;

    private DuelPhase _phase;
    private Transform[] _homeParent;   // pieces' initial parent/pose (enemy altar)
    private Vector3[] _homePos;
    private Vector3[] _homeScale;
    private readonly Queue<int> _releaseQueue = new Queue<int>(); // unlocked pieces, waiting for Jammo
    private int _releasedTotal;
    private int _delivered;
    private int _pendingRelease;
    private bool _atHome;

    private Coroutine _loop;
    private WaitForSeconds _waitPause;

    private int PieceCount => enemyPieces != null ? enemyPieces.Length : 0;

    void Awake()
    {
        _waitPause = new WaitForSeconds(pauseAfterPlace);

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

    /// <summary>Called by BossShield every 'hitsPerPiece' Player hits (collection only): unlocks the next piece to carry. Capped to the piece count.</summary>
    public void NotifyPlayerBrokePiece()
    {
        if (_releasedTotal + _pendingRelease < PieceCount) _pendingRelease++;
    }

    private IEnumerator RunDuel()
    {
        flip.ApplyImmediate(false);
        RevealHud();

        yield return PhaseRoutine(moon: false);
        if (_phase == DuelPhase.Lost) { _loop = null; yield break; }

        yield return PhaseRoutine(moon: true);
        _loop = null;
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

        ResetPiecesHome();
        _releaseQueue.Clear();
        _releasedTotal = 0;
        _delivered = 0;
        _pendingRelease = 0;
        _atHome = false;
        ReportCollect();
        if (moon) onEnterMoon.Invoke(); else onEnterSun.Invoke();

        // --- Collection: the Player unlocks pieces by hitting; Jammo carries them ---
        while (_delivered < PieceCount)
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

        if (moon) { Win(); yield break; }

        // Sun phase complete: re-shield, flip the sky (the pieces return home at the
        // start of the Moon phase, in ResetPiecesHome).
        bossShield.SetInvulnerable(true);
        _phase = DuelPhase.Flipping;
        boss.SetPaused(true);
        yield return flip.FlipTo(true);
        boss.SetPaused(false);
    }

    private void ProcessPendingReleases()
    {
        while (_pendingRelease > 0 && _releasedTotal < PieceCount)
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
            _releaseQueue.Enqueue(index); // already unlocked: Jammo retries, no extra hit
            yield return _waitPause;
            yield break;
        }

        yield return jammo.PlacePiece(piece, playerAltarSlots[index].position);
        _delivered++;
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

    private IEnumerator GoHomeOnce()
    {
        if (!_atHome && jammo.HomePost != null)
        {
            yield return jammo.WalkTo(jammo.HomePost.position);
            _atHome = true;
        }
        yield return null;
    }

    private void Win()
    {
        _phase = DuelPhase.Won;
        boss.SetPaused(true);
        onWin.Invoke();
    }

    private void Lose()
    {
        _phase = DuelPhase.Lost;
        onLose.Invoke(); // the Game Over screen is driven by PlayerHealth/JammoHealth
    }

    private bool PlayerOrJammoDown()
        => (playerHealth != null && playerHealth.IsDead) || (jammo != null && jammo.IsDead);

    private void ReportCollect()
        => onCollectProgress.Invoke(PieceCount > 0 ? Mathf.Clamp01((float)_delivered / PieceCount) : 0f);
}
