using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Orchestrates the Act 1 memory path puzzle: procedurally generates a 73-tile self-avoiding
/// path (via <see cref="SelfAvoidingPathGenerator"/> + <see cref="FuzzyPathEvaluator"/>), drives
/// Jammo's guided walk, validates the Player's steps (with a grace period and a halfway
/// checkpoint), reveals the door step by step, offers a limited memory hint, and handles
/// fall/respawn on wrong tiles.
/// </summary>
public class PathPuzzleManager : MonoBehaviour
{
    [Header("Puzzle state")]
    public bool isPuzzleActive = false;
    public bool isPlayerFalling = false;
    public bool IsPuzzleCompleted { get; private set; }

    [Header("Path settings")]
    public List<PathTile> correctPath = new List<PathTile>();

    // Derived from the generated path (exact half of 0..72 = 36). Stays public because
    // JammoGuideController reads it; hidden in the Inspector because it must not be set by hand.
    [HideInInspector] public int checkpointIndex;

    [Header("Fixed path tiles")]
    [Tooltip("Scene PathTile that always stays as the Start tile (correctPath[0]). E.g. Tile_15_15.")]
    [SerializeField] private PathTile startTile;
    [Tooltip("Scene PathTile that always stays as the End tile (correctPath[72]). E.g. Tile_13_21.")]
    [SerializeField] private PathTile endTile;
    [Tooltip("Mandated penultimate tile (correctPath[71]): fixes the arrival direction onto the final tile. Must be adjacent to endTile. E.g. Tile_12_21.")]
    [SerializeField] private PathTile endApproachTile;

    [Header("Procedural generation (Backtracking + Fuzzy)")]
    [Tooltip("Distance between the centers of two adjacent tiles. Must match TileGridGenerator's tileSize.")]
    [SerializeField] private float tileSize = 2f;
    [Tooltip("0 = random seed every Play. Non-zero = deterministic seed (useful for debugging).")]
    [SerializeField] private int randomSeed = 0;
    [Tooltip("How many candidate paths to generate: the one with the highest aesthetic (Fuzzy) score is kept.")]
    [SerializeField] private int candidateCount = 12;

    [Header("References")]
    public Transform player;
    public Transform startingPoint;
    public JammoGuideController jammoController; // The script that moves Jammo

    [Tooltip("Level's global floor — turned off at puzzle start so wrong tiles no longer support the player.")]
    [SerializeField] private GameObject globalFloor;

    [Tooltip("Seconds of falling before respawn after a wrong tile.")]
    [SerializeField] private float failResetDelay = 1.5f;

    [Header("Memory hint")]
    [Tooltip("How many times the player can use the memory hint per attempt.")]
    [SerializeField] private int memoryHintCharges = 3;
    [Tooltip("How many correct tiles ahead of the player are lit.")]
    [SerializeField] private int hintRevealCount = 8;
    [Tooltip("Seconds of steady light before the turn-off blink.")]
    [SerializeField] private float hintRevealDuration = 5f;
    [Tooltip("Number of blinks the hint turns off with.")]
    [SerializeField] private int hintBlinkCount = 3;
    [Tooltip("Duration (seconds) of each half-phase of the hint blink.")]
    [SerializeField] private float hintBlinkInterval = 0.4f;
    [Tooltip("Key (new Input System) to activate the memory hint.")]
    [SerializeField] private Key memoryHintKey = Key.Q;
    [Tooltip("CinematicFallManager: locks the player's movement (keeping look) during the hint. Same object used by JammoGuideController.")]
    [SerializeField] private CinematicFallManager cinematicManager;
    [Tooltip("Optional: invoked on every hint use (SFX, Jammo quip, etc.).")]
    public UnityEvent onHintUsed;
    [Tooltip("Optional: invoked when the hint charges run out.")]
    public UnityEvent onHintsDepleted;

    [Header("Door")]
    [Tooltip("Controller of the door that builds up in height via shader.")]
    [SerializeField] private DoorBuildController doorController;

    [Tooltip("Total number of door reveal steps (8 = 8 increments, each covering 1/8 of the height).")]
    [SerializeField] private int doorTotalSteps = 8;

    [Tooltip("How many correct tiles are needed between one door step and the next.")]
    [SerializeField] private int doorTilesPerStep = 9;

    [Tooltip("Scene door portal: auto-activated when the puzzle is completed (useful if the door is geometrically between the penultimate and last tile).")]
    [SerializeField] private DoorPortal doorPortal;

    private int currentStepIndex = 0;
    // Highest correctPath index the player has guessed in ANY attempt. Used to re-light the
    // already-discovered path after a fail and to not replay the checkpoint cinematic on retry.
    private int _maxCorrectStepReached = -1;
    private int _lastDoorStep = 0;
    // When true, off-path tiles don't make the player fall but only light up. Granted after
    // StartPuzzleSequence, FailAndReset and the end of Jammo's walks; consumed by the first
    // "first time" advance.
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
    private const int CHECKPOINT_INDEX = (TOTAL_PATH_LENGTH - 1) / 2; // 36, exact half

    /// <summary>
    /// Generates a self-avoiding 73-tile path (no crossings) from startTile to endTile via
    /// randomized backtracking, keeping the best of several candidates by Fuzzy score. The
    /// checkpoint is the halfway tile (index 36), derived automatically. Called in Start()
    /// (every Play) and from the Inspector "Generate Path!" button.
    /// </summary>
    public void GenerateRandomPath()
    {
        if (startTile == null || endTile == null || endApproachTile == null)
        {
            Debug.LogError("[PathPuzzleManager] startTile / endTile / endApproachTile are not assigned. No generation.", this);
            return;
        }

        BuildTileMap();
        if (_tileMap == null || _tileMap.Count == 0)
        {
            Debug.LogError("[PathPuzzleManager] No PathTile found in the scene.", this);
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
                $"[PathPuzzleManager] Start→End not compatible with {TOTAL_PATH_LENGTH} tiles: Manhattan(S,E)={manS_E}, moves={moves}. " +
                $"Need manS_E <= {moves} and same parity. Move startTile/endTile.", this);
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
                Debug.LogError($"[PathPuzzleManager] Generated path has {full.Count} cells, expected {TOTAL_PATH_LENGTH}.", this);
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
                    Debug.LogError($"[PathPuzzleManager] Cell {cell} (step {i}) matches no PathTile in the scene.", this);
                    correctPath.Clear();
                    return;
                }
            }

            checkpointIndex = CHECKPOINT_INDEX;
            Debug.Log($"[PathPuzzleManager] Path generated (seed={seed}, length={correctPath.Count}, checkpoint@{checkpointIndex}).");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[PathPuzzleManager] Generation failed: {ex.Message}", this);
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
        // Mathf.RoundToInt does banker's rounding (round half to even): with tile centers at
        // half of tileSize (e.g. world 1, 3, 5… with tileSize=2) two adjacent tiles would
        // collapse onto the same cell. We use explicit round half-up.
        float inv = tileSize > 0f ? 1f / tileSize : 1f;
        return new Vector2Int(
            Mathf.FloorToInt(worldPos.x * inv + 0.5f),
            Mathf.FloorToInt(worldPos.z * inv + 0.5f));
    }

    /// <summary>Starts the puzzle: enables validation, turns off the rescue floor, and sends Jammo to the checkpoint. Wired from dialogues.</summary>
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

        // NO bulk SetSolid(false): if the player is over a non-correct tile right now
        // (e.g. the tile where Jammo was activated), the floor would open under them.
        // Falling on wrong tiles happens lazily in CheckPlayerStep on the first step.

        // Turn off the "rescue floor": only the non-trigger colliders of globalFloor
        // (and its children, EXCLUDING PathTiles). No SetActive(false): if the tiles are
        // parented under globalFloor, disabling the parent would make them all disappear.
        if (globalFloor != null)
        {
            if (IsPlayerOverATile())
            {
                DisableGlobalFloorColliders();
            }
            else
            {
                Debug.LogWarning("[PathPuzzleManager] The player is not over any PathTile: leaving the global floor active to avoid an immediate fall. Move the player over a tile before activating Jammo, or extend the tile grid to cover the whole activation area.", this);
            }
        }

        // Tell Jammo to walk to the checkpoint
        jammoController.WalkToCheckpoint();
    }

    /// <summary>Validates a tile the Player stepped on: handles free exploration, on-path backtracking, advancing, grace, and fail.</summary>
    public void CheckPlayerStep(PathTile steppedTile)
    {
        // Free exploration (puzzle not active yet): just light up.
        if (!isPuzzleActive)
        {
            steppedTile.SetColor(steppedTile.playerColor);
            return;
        }

        int idx = correctPath.IndexOf(steppedTile);

        // Rule 1 — On-path backtracking: the tile was already discovered before.
        // Allow, no advance, but currentStepIndex adjusts so the next "real" step is the
        // one immediately after this position.
        if (idx >= 0 && idx <= _maxCorrectStepReached)
        {
            steppedTile.SetColor(steppedTile.playerColor);
            currentStepIndex = idx + 1;
            return;
        }

        // Rule 2 — Advance.
        // Out of grace: strict step-by-step (only idx == max + 1).
        // In grace: besides the strict step, a "milestone jump" is allowed —
        //   stepping directly on the checkpoint or final tile fast-forwards max to that
        //   index and fires the event. Other intermediate path tiles during grace do NOT
        //   advance (they fall into Rule 3 and light up blue), so holding W right after
        //   activation doesn't accidentally end grace and then fail the next tile.
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

            // Door reveal: raise the door based on the new max (it can jump several
            // steps at once if the player skipped during grace).
            if (doorController != null)
            {
                int newDoorStep = Mathf.Min((_maxCorrectStepReached + 1) / doorTilesPerStep, doorTotalSteps);
                if (newDoorStep > _lastDoorStep)
                {
                    doorController.SetProgressStep(newDoorStep, doorTotalSteps);
                    _lastDoorStep = newDoorStep;
                }
            }

            // Checkpoint fires the first time max crosses checkpointIndex.
            if (idx >= checkpointIndex && prevMax < checkpointIndex)
            {
                Debug.Log("[Jammo] Checkpoint reached! Jammo resumes.");
                jammoController.ResumeWalkToEnd();
            }
            else if (idx == correctPath.Count - 1)
            {
                Debug.Log("[Puzzle] Puzzle Completed! Open the door!");
                IsPuzzleCompleted = true;
                if (doorPortal != null) doorPortal.TriggerTransition();
            }
            return;
        }

        // Rule 3 — Off-path or skipping ahead.
        if (_inGracePeriod)
        {
            // Grace: the player is looking for the right way after respawn / dialogue /
            // Jammo's walk. Light up but don't punish.
            steppedTile.SetColor(steppedTile.playerColor);
            return;
        }

        // Not in grace and off-path: FAIL.
        steppedTile.SetColor(steppedTile.wrongColor);
        steppedTile.SetSolid(false);
        StartCoroutine(FailAndReset());
    }

    /// <summary>Turns off every tile currently lit as Jammo's (robot) trail.</summary>
    public void ClearRobotTrail()
    {
        if (_allTiles == null) return;
        foreach (PathTile tile in _allTiles)
        {
            if (tile != null) tile.ClearIfRobotLit();
        }
    }

    /// <summary>
    /// When Jammo stops (checkpoint / end) the purple trail doesn't vanish at once: it blinks
    /// then turns off, using the SAME timing as the memory hint (hintBlinkCount / hintBlinkInterval).
    /// </summary>
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

    /// <summary>
    /// Limited-charge memory hint. Can also be wired from an InteractableObject via UnityEvent,
    /// besides the key in Update().
    /// </summary>
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

    /// <summary>Lights the next stretch of correct tiles in hint color, blinks them off, then restores per-progress colors.</summary>
    private IEnumerator MemoryHintRoutine()
    {
        // Player stays put but camera/mouse stay live for the whole hint duration.
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

        // Restore consistent with progress: already-discovered tiles stay blue,
        // the others turn back off.
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

    /// <summary>Re-enables the grace period (off-path tiles light up instead of failing).</summary>
    public void GrantGracePeriod()
    {
        _inGracePeriod = true;
    }

    private bool IsPlayerOverATile()
    {
        if (player == null) return false;
        // Long downward raycast: take all hits because tiles and floor may sit at close
        // heights, and we only care that AT LEAST one is a PathTile.
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
        // Disable all non-trigger colliders of globalFloor and its children, EXCEPT those
        // that are part of a PathTile (they may be parented under it; we don't touch them).
        Collider[] cols = globalFloor.GetComponentsInChildren<Collider>(includeInactive: false);
        foreach (Collider c in cols)
        {
            if (c == null || !c.enabled) continue;
            if (c.GetComponentInParent<PathTile>() != null) continue;
            c.enabled = false;
        }
    }

    /// <summary>On a wrong step: pauses tiles, waits, teleports the player to the start, resets colors, and re-lights discovered tiles.</summary>
    private IEnumerator FailAndReset()
    {
        isPlayerFalling = true; // Pause the tiles

        // If a memory hint is in progress, stop it and give movement back:
        // the color reset below already covers the lit tiles.
        if (_hintRoutine != null)
        {
            StopCoroutine(_hintRoutine);
            _hintRoutine = null;
            if (cinematicManager != null) cinematicManager.SetPlayerMovement(true, keepLookActive: true);
        }

        yield return new WaitForSeconds(failResetDelay);

        if (player != null && startingPoint != null)
        {
            // The CharacterController overwrites transform.position with its internal copy:
            // disable it for a frame to allow the teleport.
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.position = startingPoint.position;
            if (cc != null) cc.enabled = true;
        }
        currentStepIndex = 0;

        // Reset colors + re-enable ALL solids: the lazy disable in CheckPlayerStep
        // will reopen the gap if the player repeats the mistake.
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

        // Path memory: re-light the already-guessed tiles so the player immediately sees
        // the route so far and doesn't have to re-memorize it.
        for (int i = 0; i <= _maxCorrectStepReached && i < correctPath.Count; i++)
        {
            PathTile t = correctPath[i];
            if (t != null) t.SetColor(t.playerColor);
        }

        // No grace reset here: after a respawn the puzzle is strict, wrong tiles make you
        // fall again. The player climbs back via Rule 1 (backtracking over already-discovered
        // tiles) or via the strict next-step.
        isPlayerFalling = false; // Resume the tiles
    }

    // --- EDITOR GIZMO TOOLS ---
    private void OnDrawGizmos()
    {
        if (correctPath == null || correctPath.Count == 0) return;
        for (int i = 0; i < correctPath.Count; i++)
        {
            PathTile tile = correctPath[i];
            if (tile != null)
            {
                // If it's the Checkpoint tile, color it YELLOW instead of green
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