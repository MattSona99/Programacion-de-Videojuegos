using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Orchestrates Act 2 statue assembly: waves and Jammo are decoupled. Each killed enemy queues a
/// drop (WaveManager.EnemyKilled). Jammo runs a single infinite loop: if a drop is queued and he's
/// free, he spawns ONE piece (at a random spawn point, via PieceSpawner), carries it to the
/// pedestal and fills a statue slot; if the queue is empty he returns to his home post. The arena
/// ends when the statue is complete (stops the waves).
/// </summary>
public class StatueAssemblyDirector : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WaveManager waveManager;
    [SerializeField] private StatueRig statueRig;
    [SerializeField] private PieceSpawner pieceSpawner;
    [SerializeField] private JammoCarrier jammo;

    [Tooltip("Where Jammo stands to deposit (near the pedestal).")]
    [SerializeField] private Transform placementPoint;

    [Header("Timing")]
    [SerializeField] private float pauseAfterPlace = 0.4f;

    [Header("Events (director hooks)")]
    [SerializeField] private UnityEvent onAssemblyStarted;
    [SerializeField] private UnityEvent onAssemblyFinished;

    private Coroutine _routine;
    private WaitForSeconds _waitPause;
    private int _pending;          // queued kills not yet processed
    private bool _assemblyStarted; // onAssemblyStarted only once
    private bool _atHome;          // avoids re-walking home every idle frame

    void Awake()
    {
        _waitPause = new WaitForSeconds(pauseAfterPlace);
    }

    void OnEnable()
    {
        if (waveManager == null || statueRig == null || pieceSpawner == null ||
            jammo == null || placementPoint == null)
        {
            Debug.LogWarning("[StatueAssemblyDirector] Missing references: the director won't start.", this);
            return;
        }

        waveManager.EnemyKilled += OnEnemyKilled;
        _routine = StartCoroutine(RunRoutine());
    }

    void OnDisable()
    {
        if (waveManager != null) waveManager.EnemyKilled -= OnEnemyKilled;
        if (_routine != null) { StopCoroutine(_routine); _routine = null; }
    }

    // One kill = one drop. Capped to the actually available slots: kills beyond statue
    // completion queue nothing.
    private void OnEnemyKilled()
    {
        if (statueRig != null && !statueRig.IsComplete && _pending < statueRig.RemainingCount)
            _pending++;
    }

    /// <summary>Main loop: for each queued kill, spawns a piece, has Jammo carry and place it, handling loss/death; ends when the statue is complete.</summary>
    private IEnumerator RunRoutine()
    {
        while (statueRig != null && !statueRig.IsComplete && !jammo.IsDead)
        {
            if (_pending > 0)
            {
                _pending--;
                _atHome = false;

                int idx = statueRig.TakeRandomUnfilledSlot();
                if (idx < 0) break; // safety: no more slots

                GameObject prop = pieceSpawner.SpawnPiece(statueRig.PartNameOf(idx));
                if (prop == null)
                {
                    Debug.LogWarning("[StatueAssemblyDirector] part not spawnable: slot skipped.", this);
                    statueRig.ReturnSlot(idx);
                    continue;
                }

                if (!_assemblyStarted) { _assemblyStarted = true; onAssemblyStarted.Invoke(); }

                yield return jammo.WalkTo(prop.transform.position);
                yield return jammo.PickUpRoutine(prop.transform);
                yield return jammo.WalkTo(placementPoint.position);

                // Jammo died mid-carry: leave the piece, exit the loop.
                if (jammo.IsDead)
                {
                    statueRig.ReturnSlot(idx);
                    pieceSpawner.Release(prop);
                    break;
                }

                // Hit during carry: piece LOST, slot goes back to ghost.
                if (jammo.DropRequested)
                {
                    yield return jammo.DropCarriedPiece(prop.transform);
                    statueRig.ReturnSlot(idx);
                    pieceSpawner.Release(prop);
                    jammo.ClearDropRequest();
                    yield return _waitPause;
                    continue;
                }

                yield return jammo.ReleasePiece(prop.transform);
                statueRig.OnSlotFilled(idx);
                pieceSpawner.Release(prop);

                yield return _waitPause;
            }
            else
            {
                // Empty queue: return home once, then wait.
                if (!_atHome && jammo.HomePost != null)
                {
                    yield return jammo.WalkTo(jammo.HomePost.position);
                    _atHome = true;
                }
                yield return null;
            }
        }

        // Statue complete: normal end. If instead Jammo died, do NOT fire
        // onAssemblyFinished/StopAndEnd here: JammoHealth handles it (Game Over).
        if (jammo == null || !jammo.IsDead)
        {
            onAssemblyFinished.Invoke();
            if (waveManager != null) waveManager.StopAndEnd();
        }
        _routine = null;
    }
}
