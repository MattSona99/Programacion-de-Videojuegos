using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

public class PathPuzzleManager : MonoBehaviour
{
    [Header("Stato del Puzzle")]
    public bool isPuzzleActive = false;
    public bool isPlayerFalling = false; // NUOVA VARIABILE
    public bool IsPuzzleCompleted { get; private set; }

    [Header("Impostazioni Percorso")]
    public List<PathTile> correctPath = new List<PathTile>();

    // Derivato dal percorso generato (metà esatta di 0..72 = 36). Resta public
    // perché JammoGuideController lo legge; nascosto in Inspector perché non va
    // più impostato a mano.
    [HideInInspector] public int checkpointIndex;

    [Header("Tile Fisse del Percorso")]
    [Tooltip("PathTile della scena che resta sempre come tile di Inizio (correctPath[0]). Es: Tile_15_15.")]
    [SerializeField] private PathTile startTile;
    [Tooltip("PathTile della scena che resta sempre come tile di Fine (correctPath[72]). Es: Tile_13_21.")]
    [SerializeField] private PathTile endTile;
    [Tooltip("Penultima tile obbligata (correctPath[71]): fissa la direzione d'arrivo sulla tile finale. Deve essere adiacente a endTile. Es: Tile_12_21.")]
    [SerializeField] private PathTile endApproachTile;

    [Header("Generazione Procedurale (Backtracking + Fuzzy)")]
    [Tooltip("Distanza tra i centri di due tile adiacenti. Deve combaciare con il tileSize di TileGridGenerator.")]
    [SerializeField] private float tileSize = 2f;
    [Tooltip("0 = seed random ogni Play. Valore != 0 = seed deterministico (utile in debug).")]
    [SerializeField] private int randomSeed = 0;
    [Tooltip("Quanti percorsi candidati generare: viene tenuto quello col punteggio estetico (Fuzzy) più alto.")]
    [SerializeField] private int candidateCount = 12;

    [Header("Riferimenti")]
    public Transform player;
    public Transform startingPoint;
    public JammoGuideController jammoController; // Il nuovo script per muovere Jammo

    [Tooltip("Pavimento globale del livello — viene spento all'inizio del puzzle così le tile sbagliate non sostengono più il player.")]
    [SerializeField] private GameObject globalFloor;

    [Tooltip("Secondi di caduta prima del respawn dopo una tile sbagliata.")]
    [SerializeField] private float failResetDelay = 1.5f;

    [Header("Aiuto a Memoria")]
    [Tooltip("Quante volte il player può usare l'aiuto a memoria in un tentativo.")]
    [SerializeField] private int memoryHintCharges = 3;
    [Tooltip("Quante tile corrette davanti al player vengono illuminate.")]
    [SerializeField] private int hintRevealCount = 8;
    [Tooltip("Secondi di luce fissa prima del lampeggio di spegnimento.")]
    [SerializeField] private float hintRevealDuration = 5f;
    [Tooltip("Numero di lampeggi con cui l'aiuto si spegne.")]
    [SerializeField] private int hintBlinkCount = 3;
    [Tooltip("Durata (secondi) di ogni semi-fase del lampeggio dell'aiuto.")]
    [SerializeField] private float hintBlinkInterval = 0.4f;
    [Tooltip("Tasto (nuovo Input System) per attivare l'aiuto a memoria.")]
    [SerializeField] private Key memoryHintKey = Key.Q;
    [Tooltip("CinematicFallManager: blocca il movimento del player (mantenendo il look) durante l'aiuto. Stesso oggetto usato da JammoGuideController.")]
    [SerializeField] private CinematicFallManager cinematicManager;
    [Tooltip("Opzionale: invocato a ogni uso dell'aiuto (SFX, battuta di Jammo, ecc.).")]
    public UnityEvent onHintUsed;
    [Tooltip("Opzionale: invocato quando le cariche dell'aiuto finiscono.")]
    public UnityEvent onHintsDepleted;

    [Header("Porta")]
    [Tooltip("Controller della porta che si costruisce in altezza via shader.")]
    [SerializeField] private DoorBuildController doorController;

    [Tooltip("Numero totale di step di reveal della porta (8 = 8 scatti, ognuno copre 1/8 dell'altezza).")]
    [SerializeField] private int doorTotalSteps = 8;

    [Tooltip("Quante tile giuste servono tra uno step di porta e il successivo.")]
    [SerializeField] private int doorTilesPerStep = 9;

    [Tooltip("Door portal della scena: viene attivato automaticamente quando il puzzle è completato (utile se la porta è geometricamente tra penultima e ultima tile).")]
    [SerializeField] private DoorPortal doorPortal;

    private int currentStepIndex = 0;
    // Indice più alto della correctPath che il player ha indovinato in QUALUNQUE
    // tentativo. Ci serve per ri-accendere il percorso già scoperto dopo un fail
    // e per non far ripartire la cinematica del checkpoint al retry.
    private int _maxCorrectStepReached = -1;
    private int _lastDoorStep = 0;
    // Quando true, le tile fuori percorso non fanno cadere il player ma si
    // illuminano e basta. Concesso dopo StartPuzzleSequence, FailAndReset e fine
    // delle camminate di Jammo; consumato dal primo avanzamento "first time".
    private bool _inGracePeriod = true;
    private HashSet<PathTile> _correctSet;
    private PathTile[] _allTiles;

    private Dictionary<Vector2Int, PathTile> _tileMap;
    private RectInt _gridBounds;

    private int _hintChargesLeft;
    private Coroutine _hintRoutine;

    void Start()
    {
        GenerateRandomPath();
    }

    void Update()
    {
        if (!isPuzzleActive || isPlayerFalling || _hintRoutine != null) return;
        if (jammoController != null && jammoController.IsWalking) return;
        if (Keyboard.current == null) return;
        if (Keyboard.current[memoryHintKey].wasPressedThisFrame) UseMemoryHint();
    }

    private const int TOTAL_PATH_LENGTH = 73;
    private const int CHECKPOINT_INDEX = (TOTAL_PATH_LENGTH - 1) / 2; // 36, metà esatta

    // Genera con backtracking randomizzato un percorso self-avoiding (niente
    // incroci) di 73 tile da startTile a endTile, scegliendo tra più candidati
    // quello col punteggio Fuzzy migliore. Il Checkpoint è la tile a metà
    // percorso (indice 36), derivata automaticamente.
    // Chiamato in Start() (ogni Play) e dal bottone Inspector "Genera Percorso!".
    public void GenerateRandomPath()
    {
        if (startTile == null || endTile == null || endApproachTile == null)
        {
            Debug.LogError("[PathPuzzleManager] startTile / endTile / endApproachTile non sono assegnate. Niente generazione.", this);
            return;
        }

        BuildTileMap();
        if (_tileMap == null || _tileMap.Count == 0)
        {
            Debug.LogError("[PathPuzzleManager] Nessuna PathTile trovata nella scena.", this);
            return;
        }

        Vector2Int sCell = WorldToCell(startTile.transform.position);
        Vector2Int eCell = WorldToCell(endTile.transform.position);
        Vector2Int pCell = WorldToCell(endApproachTile.transform.position);

        int manS_E = Mathf.Abs(eCell.x - sCell.x) + Mathf.Abs(eCell.y - sCell.y);
        int moves = TOTAL_PATH_LENGTH - 1;
        if (manS_E > moves || ((moves - manS_E) & 1) != 0)
        {
            Debug.LogError(
                $"[PathPuzzleManager] Start→End non compatibile con {TOTAL_PATH_LENGTH} tile: Manhattan(S,E)={manS_E}, mosse={moves}. " +
                $"Servono manS_E <= {moves} e stessa parità. Sposta startTile/endTile.", this);
            return;
        }

        int seed = randomSeed != 0 ? randomSeed : System.Environment.TickCount;
        System.Random rng = new System.Random(seed);

        HashSet<Vector2Int> allowed = new HashSet<Vector2Int>(_tileMap.Keys);
        SelfAvoidingPathGenerator generator = new SelfAvoidingPathGenerator();
        FuzzyPathEvaluator fuzzy = new FuzzyPathEvaluator();

        try
        {
            GridPath full = generator.Generate(
                sCell, eCell, pCell, TOTAL_PATH_LENGTH, allowed, _gridBounds, fuzzy, rng, candidateCount);

            if (full.Count != TOTAL_PATH_LENGTH)
            {
                Debug.LogError($"[PathPuzzleManager] Path generato ha {full.Count} celle, atteso {TOTAL_PATH_LENGTH}.", this);
                return;
            }

            correctPath.Clear();
            for (int i = 0; i < full.Cells.Count; i++)
            {
                Vector2Int cell = full.Cells[i];
                if (_tileMap.TryGetValue(cell, out PathTile t))
                {
                    correctPath.Add(t);
                }
                else
                {
                    Debug.LogError($"[PathPuzzleManager] Cella {cell} (step {i}) non corrisponde a nessuna PathTile in scena.", this);
                    correctPath.Clear();
                    return;
                }
            }

            checkpointIndex = CHECKPOINT_INDEX;
            Debug.Log($"[PathPuzzleManager] Percorso generato (seed={seed}, lunghezza={correctPath.Count}, checkpoint@{checkpointIndex}).");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[PathPuzzleManager] Generazione fallita: {ex.Message}", this);
        }
    }

    private void BuildTileMap()
    {
        PathTile[] allTiles = FindObjectsByType<PathTile>(FindObjectsSortMode.None);
        _tileMap = new Dictionary<Vector2Int, PathTile>(allTiles.Length);
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;

        foreach (PathTile tile in allTiles)
        {
            if (tile == null) continue;
            Vector2Int cell = WorldToCell(tile.transform.position);
            _tileMap[cell] = tile;
            if (cell.x < minX) minX = cell.x;
            if (cell.x > maxX) maxX = cell.x;
            if (cell.y < minY) minY = cell.y;
            if (cell.y > maxY) maxY = cell.y;
        }

        if (_tileMap.Count == 0)
        {
            _gridBounds = new RectInt(0, 0, 0, 0);
            return;
        }
        _gridBounds = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    private Vector2Int WorldToCell(Vector3 worldPos)
    {
        // Mathf.RoundToInt fa banker's rounding (round half to even): con tile center
        // a metà di tileSize (es. world 1, 3, 5… con tileSize=2) due tile adiacenti
        // verrebbero collassate sulla stessa cella. Usiamo round half-up esplicito.
        float inv = tileSize > 0f ? 1f / tileSize : 1f;
        return new Vector2Int(
            Mathf.FloorToInt(worldPos.x * inv + 0.5f),
            Mathf.FloorToInt(worldPos.z * inv + 0.5f));
    }

    // Questa funzione verrà chiamata dai tuoi dialoghi per iniziare il gioco!
    public void StartPuzzleSequence()
    {
        isPuzzleActive = true;
        currentStepIndex = 0;
        _maxCorrectStepReached = -1;
        _lastDoorStep = 0;
        _inGracePeriod = true;
        IsPuzzleCompleted = false;
        _hintChargesLeft = memoryHintCharges;

        _correctSet = new HashSet<PathTile>(correctPath);
        _allTiles = FindObjectsByType<PathTile>(FindObjectsSortMode.None);

        // NIENTE bulk SetSolid(false): se il player è sopra una tile non-corretta in questo
        // istante (es. la tile dove ha attivato Jammo), gli si aprirebbe il pavimento sotto.
        // La caduta sulle tile sbagliate avviene lazy in CheckPlayerStep al primo passo.

        // Spegniamo il "pavimento di soccorso": solo i collider non-trigger del globalFloor
        // (e dei suoi figli, ESCLUSE le PathTile). Niente SetActive(false): se le tile sono
        // parented sotto globalFloor, disattivare il parent le farebbe sparire tutte.
        if (globalFloor != null)
        {
            if (IsPlayerOverATile())
            {
                DisableGlobalFloorColliders();
            }
            else
            {
                Debug.LogWarning("[PathPuzzleManager] Il player non risulta sopra nessuna PathTile: lascio attivo il pavimento globale per evitare la caduta immediata. Sposta il player sopra una tile prima di attivare Jammo, oppure estendi la griglia di tile a coprire tutta l'area di attivazione.", this);
            }
        }

        // Diciamo a Jammo di camminare fino al checkpoint
        jammoController.WalkToCheckpoint();
    }

    public void CheckPlayerStep(PathTile steppedTile)
    {
        // Esplorazione libera (puzzle non ancora attivo): illumina e basta.
        if (!isPuzzleActive)
        {
            steppedTile.SetColor(steppedTile.playerColor);
            return;
        }

        int idx = correctPath.IndexOf(steppedTile);

        // Regola 1 — Backtracking on-path: la tile è già stata scoperta in passato.
        // Allow, niente advance, ma currentStepIndex si adegua così il prossimo
        // step "vero" è quello immediatamente successivo a questa posizione.
        if (idx >= 0 && idx <= _maxCorrectStepReached)
        {
            steppedTile.SetColor(steppedTile.playerColor);
            currentStepIndex = idx + 1;
            return;
        }

        // Regola 2 — Avanzamento.
        // Fuori grace: step-by-step rigoroso (solo idx == max + 1).
        // In grace: oltre allo step rigoroso, è ammesso il "salto a milestone" —
        //   calpestare direttamente la tile di checkpoint o quella finale fa
        //   fast-forward di max a quell'indice e fa scattare l'evento. Le altre tile
        //   intermedie della path durante la grace NON avanzano (cadrebbero in Rule 3
        //   e si illuminano blu), così holdare W subito dopo l'attivazione non chiude
        //   accidentalmente la grace facendo poi fallire la tile successiva.
        int endIdx = correctPath.Count - 1;
        bool isNextStep = idx == _maxCorrectStepReached + 1;
        bool isGraceMilestone = _inGracePeriod
            && idx > _maxCorrectStepReached
            && (idx == checkpointIndex || idx == endIdx);

        if (idx >= 0 && (isNextStep || isGraceMilestone))
        {
            int prevMax = _maxCorrectStepReached;

            steppedTile.SetColor(steppedTile.playerColor);
            _maxCorrectStepReached = idx;
            currentStepIndex = idx + 1;
            _inGracePeriod = false;

            // Door reveal: alziamo la porta in base al nuovo max (può saltare di
            // più step in un colpo se il player ha skippato in grace).
            if (doorController != null)
            {
                int newDoorStep = Mathf.Min((_maxCorrectStepReached + 1) / doorTilesPerStep, doorTotalSteps);
                if (newDoorStep > _lastDoorStep)
                {
                    doorController.SetProgressStep(newDoorStep, doorTotalSteps);
                    _lastDoorStep = newDoorStep;
                }
            }

            // Checkpoint scatta la prima volta che max attraversa checkpointIndex.
            if (idx >= checkpointIndex && prevMax < checkpointIndex)
            {
                Debug.Log("[Jammo] Checkpoint raggiunto! Jammo riparte.");
                jammoController.ResumeWalkToEnd();
            }
            else if (idx == correctPath.Count - 1)
            {
                Debug.Log("[Puzzle] Puzzle Completato! Apri la porta!");
                IsPuzzleCompleted = true;
                if (doorPortal != null) doorPortal.TriggerTransition();
            }
            return;
        }

        // Regola 3 — Off-path o skipping ahead.
        if (_inGracePeriod)
        {
            // Grace: il player sta cercando la strada giusta dopo respawn / dialogo /
            // camminata di Jammo. Illumina ma non punire.
            steppedTile.SetColor(steppedTile.playerColor);
            return;
        }

        // Non in grace e fuori percorso: FAIL.
        steppedTile.SetColor(steppedTile.wrongColor);
        steppedTile.SetSolid(false);
        StartCoroutine(FailAndReset());
    }

    public void ClearRobotTrail()
    {
        if (_allTiles == null) return;
        foreach (PathTile tile in _allTiles)
        {
            if (tile != null) tile.ClearIfRobotLit();
        }
    }

    // Quando Jammo si ferma (checkpoint / fine) la scia viola non sparisce di
    // colpo: lampeggia e poi si spegne, con lo STESSO timing dell'aiuto a
    // memoria (hintBlinkCount / hintBlinkInterval).
    public IEnumerator BlinkAndClearRobotTrail()
    {
        if (_allTiles == null) yield break;

        List<PathTile> lit = new List<PathTile>();
        foreach (PathTile tile in _allTiles)
        {
            if (tile != null && tile.IsRobotLit) lit.Add(tile);
        }
        if (lit.Count == 0) yield break;

        WaitForSeconds wait = new WaitForSeconds(hintBlinkInterval);
        for (int i = 0; i < hintBlinkCount; i++)
        {
            foreach (PathTile tile in lit) if (tile != null) tile.SetColor(tile.defaultColor);
            yield return wait;
            foreach (PathTile tile in lit) if (tile != null) tile.SetColor(tile.robotColor);
            yield return wait;
        }

        ClearRobotTrail();
    }

    // Aiuto a memoria a cariche limitate. Agganciabile anche da un
    // InteractableObject via UnityEvent, oltre al tasto in Update().
    public void UseMemoryHint()
    {
        if (!isPuzzleActive || isPlayerFalling) return;
        if (jammoController != null && jammoController.IsWalking) return;
        if (_hintRoutine != null) return;
        if (_hintChargesLeft <= 0) return;

        _hintChargesLeft--;
        onHintUsed?.Invoke();
        if (_hintChargesLeft <= 0) onHintsDepleted?.Invoke();

        _hintRoutine = StartCoroutine(MemoryHintRoutine());
    }

    private IEnumerator MemoryHintRoutine()
    {
        // Player fermo ma camera/mouse vivi per tutta la durata dell'aiuto.
        if (cinematicManager != null) cinematicManager.SetPlayerMovement(false, keepLookActive: true);

        int from = _maxCorrectStepReached + 1;
        int to = Mathf.Min(from + hintRevealCount - 1, correctPath.Count - 1);

        for (int i = from; i <= to; i++)
        {
            PathTile t = correctPath[i];
            if (t != null) t.SetColor(t.hintColor);
        }

        yield return new WaitForSeconds(hintRevealDuration);

        WaitForSeconds blinkWait = new WaitForSeconds(hintBlinkInterval);
        for (int b = 0; b < hintBlinkCount; b++)
        {
            for (int i = from; i <= to; i++)
            {
                PathTile t = correctPath[i];
                if (t != null) t.SetColor(t.defaultColor);
            }
            yield return blinkWait;
            for (int i = from; i <= to; i++)
            {
                PathTile t = correctPath[i];
                if (t != null) t.SetColor(t.hintColor);
            }
            yield return blinkWait;
        }

        // Ripristino coerente col progresso: tile già scoperte restano blu,
        // le altre tornano spente.
        for (int i = from; i <= to; i++)
        {
            PathTile t = correctPath[i];
            if (t == null) continue;
            if (i <= _maxCorrectStepReached) t.SetColor(t.playerColor);
            else t.ResetTile();
        }

        if (cinematicManager != null) cinematicManager.SetPlayerMovement(true, keepLookActive: true);
        _hintRoutine = null;
    }

    public void GrantGracePeriod()
    {
        _inGracePeriod = true;
    }

    private bool IsPlayerOverATile()
    {
        if (player == null) return false;
        // Raycast lungo verso il basso: prendiamo tutti gli hit perché tile e pavimento
        // potrebbero stare a quote vicine, e ci interessa che ALMENO uno sia una PathTile.
        Vector3 origin = player.position + Vector3.up * 1.0f;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 5f, ~0, QueryTriggerInteraction.Collide);
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider != null && hit.collider.GetComponentInParent<PathTile>() != null) return true;
        }
        return false;
    }

    private void DisableGlobalFloorColliders()
    {
        if (globalFloor == null) return;
        // Spegne tutti i collider non-trigger di globalFloor e dei suoi figli, EXCEPT quelli
        // che fanno parte di una PathTile (potrebbero essere parented sotto, non li tocchiamo).
        Collider[] cols = globalFloor.GetComponentsInChildren<Collider>(includeInactive: false);
        foreach (Collider c in cols)
        {
            if (c == null || !c.enabled) continue;
            if (c.GetComponentInParent<PathTile>() != null) continue;
            c.enabled = false;
        }
    }

    private IEnumerator FailAndReset()
    {
        isPlayerFalling = true; // Blocchiamo le tile

        // Se un aiuto a memoria è in corso lo interrompiamo e ridiamo il
        // movimento: il reset colori sotto copre già le tile illuminate.
        if (_hintRoutine != null)
        {
            StopCoroutine(_hintRoutine);
            _hintRoutine = null;
            if (cinematicManager != null) cinematicManager.SetPlayerMovement(true, keepLookActive: true);
        }

        yield return new WaitForSeconds(failResetDelay);

        if (player != null && startingPoint != null)
        {
            // Il CharacterController sovrascrive transform.position con la sua copia interna:
            // va spento un frame per permettere il teleport.
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.position = startingPoint.position;
            if (cc != null) cc.enabled = true;
        }
        currentStepIndex = 0;

        // Reset colori + riaccensione di TUTTI i solidi: la disable lazy in CheckPlayerStep
        // si occuperà di riaprire il vuoto se il player rifà l'errore.
        if (_allTiles != null)
        {
            foreach (PathTile tile in _allTiles)
            {
                if (tile == null) continue;
                tile.ResetTile();
                tile.SetSolid(true);
            }
        }
        else
        {
            foreach (PathTile tile in correctPath) { if (tile != null) tile.ResetTile(); }
        }

        // Memoria del percorso: ri-accendiamo le tile già indovinate così il
        // player vede subito la strada fatta finora e non deve ri-ricordarla.
        for (int i = 0; i <= _maxCorrectStepReached && i < correctPath.Count; i++)
        {
            PathTile t = correctPath[i];
            if (t != null) t.SetColor(t.playerColor);
        }

        // Niente reset della grace qui: dopo un respawn il puzzle è strict, le tile
        // sbagliate fanno cadere di nuovo. Il player risale via Rule 1 (backtracking
        // sulle tile già scoperte) o via il next-step rigoroso.
        isPlayerFalling = false; // Sblocchiamo le tile
    }

    // --- STRUMENTI PER L'EDITOR GIZMOS ---
    private void OnDrawGizmos()
    {
        if (correctPath == null || correctPath.Count == 0) return;
        for (int i = 0; i < correctPath.Count; i++)
        {
            PathTile tile = correctPath[i];
            if (tile != null)
            {
                // Se è la tile del Checkpoint, la coloriamo di GIALLO invece che verde
                Gizmos.color = (i == checkpointIndex) ? new Color(1f, 1f, 0f, 0.8f) : new Color(0f, 1f, 0f, 0.5f); 
                
                Collider col = tile.GetComponent<Collider>();
                if (col != null) Gizmos.DrawCube(col.bounds.center, col.bounds.size);

                if (i > 0 && correctPath[i - 1] != null)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawLine(correctPath[i - 1].transform.position, tile.transform.position);
                }
            }
        }
    }
}