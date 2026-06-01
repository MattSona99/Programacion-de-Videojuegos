using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Orchestratore del livello finale "Lo Specchio d'Acqua". Due fasi IDENTICHE
// (Sole, poi Luna), ognuna in due tempi:
//   1) Raccolta: il nemico è immune; ogni 'hitsPerPiece' colpi del Player
//      (BossShield) **sblocca** uno dei pezzi GIÀ PIAZZATI sull'altare nemico.
//      Jammo porta i pezzi sbloccati all'altare del Player (uno slot per pezzo).
//   2) Finestra danno: consegnati tutti i pezzi, il nemico diventa colpibile.
//      Fase Sole → fino a 'phase1DamageFloor' (50%); Fase Luna → fino a 0 (Win).
//
// Differenze Fase Luna: il boss è potenziato (BossDuelAI) e può intercettare
// Jammo mentre trasporta — se colpito, il pezzo TORNA al suo posto sull'altare
// nemico (e Jammo lo riproverà) e Jammo subisce danno. In Fase Sole Jammo è al
// sicuro (il boss non lo prende).
//
// Sconfitta: morte del Player o di Jammo (Game Over guidato da PlayerHealth/
// JammoHealth). Riusa JammoCarrier come l'Act_02. I pezzi sono oggetti di scena
// persistenti (5 prefab diversi): niente pooling, si riposizionano e basta.
public class MirrorDuelDirector : MonoBehaviour
{
    private enum DuelPhase { Sun, Flipping, Moon, Won, Lost }

    [Header("Attori")]
    [SerializeField] private JammoCarrier jammo;
    [SerializeField] private BossDuelAI boss;
    [SerializeField] private BossShield bossShield;
    [SerializeField] private Health bossHealth;
    [SerializeField] private Health playerHealth;
    [SerializeField] private MirrorFlipDirector flip;

    [Header("Pezzi (pre-piazzati sull'altare nemico)")]
    [Tooltip("I prefab già in scena sull'altare nemico. Jammo li porta uno per uno all'altare del Player.")]
    [SerializeField] private Transform[] enemyPieces;
    [Tooltip("Slot di destinazione sull'altare del Player, uno per pezzo (stesso indice di enemyPieces).")]
    [SerializeField] private Transform[] playerAltarSlots;

    [Header("Punti d'appoggio Jammo (a terra, su NavMesh)")]
    [Tooltip("Dove Jammo si ferma per prendere i pezzi (davanti all'altare nemico). Se vuoto usa la posizione del pezzo: rischia di farlo entrare nel piedistallo.")]
    [SerializeField] private Transform enemyStandPoint;
    [Tooltip("Dove Jammo si ferma per posare i pezzi (davanti all'altare del Player). Se vuoto usa lo slot.")]
    [SerializeField] private Transform playerStandPoint;

    [Header("HUD")]
    [Tooltip("Barre HUD (HudReveal) rivelate in dissolvenza all'avvio del duello.")]
    [SerializeField] private HudReveal[] hudReveals;

    [Header("Regole")]
    [Tooltip("Vita normalizzata sotto cui il nemico NON scende in Fase Sole (0.5 = 50%). Fase Luna → 0.")]
    [Range(0f, 1f)]
    [SerializeField] private float phase1DamageFloor = 0.5f;
    [SerializeField] private float pauseAfterPlace = 0.4f;

    [Header("Eventi")]
    [SerializeField] private UnityEvent onEnterSun;
    [SerializeField] private UnityEvent onEnterMoon;
    [SerializeField] private UnityEvent onWin;
    [SerializeField] private UnityEvent onLose;
    [Tooltip("0..1: pezzi consegnati / totale (barra collezione). La barra vita Boss si aggancia a bossHealth.onDamaged.")]
    [SerializeField] private HealthChangedEvent onCollectProgress;

    private DuelPhase _phase;
    private Transform[] _homeParent;   // parent/posa iniziale dei pezzi (altare nemico)
    private Vector3[] _homePos;
    private Vector3[] _homeScale;
    private readonly Queue<int> _releaseQueue = new Queue<int>(); // pezzi sbloccati, in attesa di Jammo
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

    // Avvio in Start (non in OnEnable): RevealHud() rivela barre HudReveal il cui
    // hiddenOnStart le nasconde in Awake. Start è garantito dopo TUTTI gli Awake,
    // quindi le barre sono inizializzate quando le riveliamo (in OnEnable il
    // reveal poteva girare prima del loro Awake e venire poi sovrascritto).
    void Start()
    {
        if (jammo == null || boss == null || bossShield == null || bossHealth == null ||
            flip == null || PieceCount == 0 ||
            playerAltarSlots == null || playerAltarSlots.Length < PieceCount)
        {
            Debug.LogWarning("[MirrorDuelDirector] Riferimenti mancanti o slot insufficienti: il duello non parte.", this);
            return;
        }
        _loop = StartCoroutine(RunDuel());
    }

    void OnDisable()
    {
        if (_loop != null) { StopCoroutine(_loop); _loop = null; }
    }

    // BossShield ogni 'hitsPerPiece' colpi del Player (solo in raccolta): sblocca
    // il prossimo pezzo da portare. Cap al numero di pezzi.
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

        // Raccolta: il boss NON è invulnerabile, ma prende 1 HP/colpo (mentre
        // Jammo trasporta) e non scende sotto il floor di fase (Fase 1 = 50%).
        bossShield.SetInvulnerable(false);   // libera l'eventuale blockAll del flip
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

        // --- Raccolta: il Player sblocca i pezzi colpendo; Jammo li trasporta ---
        while (_delivered < PieceCount)
        {
            if (PlayerOrJammoDown()) { Lose(); yield break; }

            ProcessPendingReleases();

            if (_releaseQueue.Count > 0) yield return CarryOnePiece(_releaseQueue.Dequeue(), moon);
            else yield return GoHomeOnce(); // attende i colpi del Player
        }

        // --- Finestra danno: danno pieno (niente più cap a 1) ---
        bossShield.SetShedding(false);
        bossHealth.SetMaxDamagePerHit(0f); // 0 = nessun cap; il floor di fase resta

        float floor = moon ? 0f : phase1DamageFloor;
        while (!bossHealth.IsDead && bossHealth.Normalized > floor)
        {
            if (PlayerOrJammoDown()) { Lose(); yield break; }
            yield return null;
        }

        if (moon) { Win(); yield break; }

        // Fase Sole completata: ri-scuda, ribalta il cielo (i pezzi tornano a casa
        // all'inizio della Fase Luna, in ResetPiecesHome).
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

        // Jammo si ferma a terra davanti all'altare (non sopra il pezzo, che è in
        // cima al piedistallo): il pezzo poi vola su di lui in PickUpRoutine.
        Vector3 pickupStand = enemyStandPoint != null ? enemyStandPoint.position : piece.position;
        Vector3 dropStand = playerStandPoint != null ? playerStandPoint.position : playerAltarSlots[index].position;

        yield return jammo.WalkTo(pickupStand);
        yield return jammo.PickUpRoutine(piece);
        yield return jammo.WalkTo(dropStand);

        if (jammo.IsDead) yield break;

        // Solo Fase Luna: colpito mentre trasporta → pezzo perso, torna al suo posto.
        if (moon && jammo.DropRequested)
        {
            yield return jammo.DropCarriedPiece(piece);
            jammo.ClearDropRequest();
            ReturnPieceHome(index);
            _releaseQueue.Enqueue(index); // già sbloccato: Jammo riprova, niente colpo extra
            yield return _waitPause;
            yield break;
        }

        yield return jammo.PlacePiece(piece, playerAltarSlots[index].position);
        _delivered++;
        ReportCollect();
        yield return _waitPause;
    }

    // Riporta un pezzo alla sua posa iniziale sull'altare nemico (parent/pos/scala).
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
        onLose.Invoke(); // la schermata di Game Over la guida PlayerHealth/JammoHealth
    }

    private bool PlayerOrJammoDown()
        => (playerHealth != null && playerHealth.IsDead) || (jammo != null && jammo.IsDead);

    private void ReportCollect()
        => onCollectProgress.Invoke(PieceCount > 0 ? Mathf.Clamp01((float)_delivered / PieceCount) : 0f);
}
