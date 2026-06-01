using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// Orchestratore #5 (Modello 2, redesign): wave e Jammo sono disaccoppiati.
// Ogni nemico ucciso accoda un drop (WaveManager.EnemyKilled). Jammo gira in
// un unico loop infinito: se c'è un drop in coda ed è libero, spawna UN solo
// pezzo (su uno spawn point a caso, via PieceSpawner), lo porta al piedistallo
// e riempie uno slot della statua; se la coda è vuota torna a riposo all'home.
// L'arena finisce quando la statua è completa (stop alle wave).
public class StatueAssemblyDirector : MonoBehaviour
{
    [Header("Riferimenti")]
    [SerializeField] private WaveManager waveManager;
    [SerializeField] private StatueRig statueRig;
    [SerializeField] private PieceSpawner pieceSpawner;
    [SerializeField] private JammoCarrier jammo;

    [Tooltip("Dove Jammo si posiziona per depositare (vicino al piedistallo).")]
    [SerializeField] private Transform placementPoint;

    [Header("Tempi")]
    [SerializeField] private float pauseAfterPlace = 0.4f;

    [Header("Eventi (hook #6)")]
    [SerializeField] private UnityEvent onAssemblyStarted;
    [SerializeField] private UnityEvent onAssemblyFinished;

    private Coroutine _routine;
    private WaitForSeconds _waitPause;
    private int _pending;          // kill in coda non ancora processati
    private bool _assemblyStarted; // onAssemblyStarted una sola volta
    private bool _atHome;          // evita di ricamminare a casa ogni frame idle

    void Awake()
    {
        _waitPause = new WaitForSeconds(pauseAfterPlace);
    }

    void OnEnable()
    {
        if (waveManager == null || statueRig == null || pieceSpawner == null ||
            jammo == null || placementPoint == null)
        {
            Debug.LogWarning("[StatueAssemblyDirector] Riferimenti mancanti: il director non parte.", this);
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

    // Un kill = un drop. Cappato agli slot realmente disponibili: i kill
    // oltre il completamento della statua non accodano nulla.
    private void OnEnemyKilled()
    {
        if (statueRig != null && !statueRig.IsComplete && _pending < statueRig.RemainingCount)
            _pending++;
    }

    private IEnumerator RunRoutine()
    {
        while (statueRig != null && !statueRig.IsComplete && !jammo.IsDead)
        {
            if (_pending > 0)
            {
                _pending--;
                _atHome = false;

                int idx = statueRig.TakeRandomUnfilledSlot();
                if (idx < 0) break; // safety: niente più slot

                GameObject prop = pieceSpawner.SpawnPiece(statueRig.PartNameOf(idx));
                if (prop == null)
                {
                    Debug.LogWarning("[StatueAssemblyDirector] parte non spawnabile: slot saltato.", this);
                    statueRig.ReturnSlot(idx);
                    continue;
                }

                if (!_assemblyStarted) { _assemblyStarted = true; onAssemblyStarted.Invoke(); }

                yield return jammo.WalkTo(prop.transform.position);
                yield return jammo.PickUpRoutine(prop.transform);
                yield return jammo.WalkTo(placementPoint.position);

                // Jammo morto a metà trasporto: lascia il pezzo, esci dal loop.
                if (jammo.IsDead)
                {
                    statueRig.ReturnSlot(idx);
                    pieceSpawner.Release(prop);
                    break;
                }

                // Colpito durante il trasporto: pezzo PERSO, slot torna fantasma.
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
                // Coda vuota: torna a riposo una volta sola, poi attende.
                if (!_atHome && jammo.HomePost != null)
                {
                    yield return jammo.WalkTo(jammo.HomePost.position);
                    _atHome = true;
                }
                yield return null;
            }
        }

        // Statua completa: fine normale. Se invece Jammo è morto, NON lanciamo
        // onAssemblyFinished/StopAndEnd qui: ci pensa JammoHealth (Game Over).
        if (jammo == null || !jammo.IsDead)
        {
            onAssemblyFinished.Invoke();
            if (waveManager != null) waveManager.StopAndEnd();
        }
        _routine = null;
    }
}
