using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class WaveIndexEvent : UnityEvent<int> { }

// Motore delle ondate di Act_02. Guidato dall'esterno: l'orchestratore-regia
// (dialoghi/camera/pause del sotto-progetto #6) chiamerà StartNextWave() dopo i
// suoi beat. Il #5 si aggancia a onWaveCleared per il drop del pezzo / Jammo.
public class WaveManager : MonoBehaviour
{
    [System.Serializable]
    public class Wave
    {
        [Tooltip("Transform della parete da cui spawna questa ondata (orientato verso l'arena).")]
        public Transform spawnWall;
        public GameObject enemyPrefab;
        public int count = 5;
        [Tooltip("Secondi tra uno spawn e l'altro all'interno della wave.")]
        public float spawnInterval = 0.5f;
        [Tooltip("Dispersione laterale lungo la parete (asse right del Transform).")]
        public float spawnSpread = 6f;
        [Tooltip("Profondità minima di spawn verso l'arena: tiene i nemici staccati dalla parete, mai a filo del muro.")]
        public float spawnMinDepth = 2f;
        [Tooltip("Profondità di spawn verso l'arena (asse forward del Transform).")]
        public float spawnDepth = 4f;
    }

    [Header("Ondate (una parete per wave)")]
    [SerializeField] private Wave[] waves;

    [Header("Distribuzione spawn")]
    [Tooltip("Distanza minima tra due nemici della stessa wave (anti-overlap).")]
    [SerializeField] private float minSpawnSeparation = 1.5f;
    [Tooltip("Tentativi di ricollocazione prima di accettare comunque la posizione.")]
    [SerializeField] private int spawnPlacementTries = 10;

    [Header("Pooling")]
    [Tooltip("Secondi prima di rimettere il cadavere nel pool (tempo per l'anim di morte).")]
    [SerializeField] private float corpseDelay = 1.5f;

    [Header("Drop salute")]
    [Tooltip("Prefab di HealthPickup (cura il Player) spawnato alla morte di un nemico. Lascia vuoto per disabilitare i drop.")]
    [SerializeField] private GameObject healthPickupPrefab;
    [Tooltip("Prefab di HealthPickup configurato su Jammo (+25 a Jammo, colore diverso). Stessa probabilità di spawn del pickup del Player. Lascia vuoto per disabilitare.")]
    [SerializeField] private GameObject jammoHealthPickupPrefab;
    [Tooltip("Probabilità 0..1 che un nemico ucciso droppi UN pickup (poi se ne sceglie il tipo: Player o Jammo).")]
    [SerializeField, Range(0f, 1f)] private float healthDropChance = 0.25f;
    [Tooltip("Quando avviene un drop e sono assegnati entrambi i prefab, probabilità 0..1 che sia il pickup di JAMMO (il resto è il pickup del Player). 0.5 = 50/50.")]
    [SerializeField, Range(0f, 1f)] private float jammoDropShare = 0.5f;
    [Tooltip("Altezza (Y) costante a cui spawna il pickup, indipendentemente dalla posa del cadavere. Evita drop dentro al pavimento quando le anim di morte abbassano il nemico.")]
    [SerializeField] private float healthDropHeightY = 1f;

    [Header("Loop ondate")]
    [Tooltip("Le wave si ripetono in loop (indice % numero wave). L'arena finisce quando la statua è completa, non per numero di wave.")]
    [SerializeField] private bool loopWaves = true;
    [Tooltip("Avvia la prima wave da sé all'avvio (insieme a Jammo).")]
    [SerializeField] private bool startOnPlay = true;
    [Tooltip("Auto-avanza alla wave dopo (oltre al loop). Lascia OFF se guida un orchestratore esterno e loop OFF.")]
    [SerializeField] private bool autoAdvance = false;
    [Tooltip("Pausa tra una wave ripulita e la successiva.")]
    [SerializeField] private float delayBetweenWaves = 2f;
    [Tooltip("Rete di sicurezza: se una wave non viene ripulita entro N secondi, i nemici rimasti vengono rimessi nel pool e la wave si chiude (anti-softlock). 0 = disattivata.")]
    [SerializeField] private float maxWaveDuration = 0f;

    [Header("Eventi")]
    [SerializeField] private WaveIndexEvent onWaveStarted;
    [SerializeField] private WaveIndexEvent onWaveCleared;
    [SerializeField] private UnityEvent onAllWavesCleared;

    private readonly Dictionary<GameObject, SimplePool> _pools = new Dictionary<GameObject, SimplePool>();
    private readonly Dictionary<GameObject, SimplePool> _ownerPool = new Dictionary<GameObject, SimplePool>();
    private readonly HashSet<GameObject> _wired = new HashSet<GameObject>();
    private readonly List<Vector3> _spawnPositions = new List<Vector3>();
    private readonly List<GameObject> _aliveEnemies = new List<GameObject>();
    private readonly Dictionary<GameObject, SimplePool> _pickupPools = new Dictionary<GameObject, SimplePool>();
    private Coroutine _timeoutRoutine;

    private int _currentWaveIndex = -1;
    private int _aliveCount;
    private bool _waveActive;
    private bool _spawning;
    private bool _ended;

    public int CurrentWaveIndex => _currentWaveIndex;
    public bool WaveActive => _waveActive;
    public bool AllWavesDone => _ended || (!loopWaves && _currentWaveIndex >= waves.Length - 1 && !_waveActive);

    // Ogni nemico ucciso (l'unico modo per cui un nemico muore): il director
    // della statua si aggancia qui per accodare un drop.
    public event System.Action EnemyKilled;

    void Start()
    {
        if (startOnPlay) StartNextWave();
    }

    // Avvia la prossima wave. Una alla volta (gate _waveActive). In loop
    // l'indice gira su % waves.Length; si ferma solo a fine arena (_ended).
    public void StartNextWave()
    {
        if (_waveActive || _ended || waves == null || waves.Length == 0) return;

        int next;
        if (_currentWaveIndex >= waves.Length - 1)
        {
            if (!loopWaves) return;
            next = 0;
        }
        else next = _currentWaveIndex + 1;

        _currentWaveIndex = next;
        StartCoroutine(SpawnWaveRoutine(waves[next], next));
    }

    // Chiamato dal director quando la statua è completa: stop definitivo.
    public void StopAndEnd()
    {
        if (_ended) return;
        _ended = true;
        StopAllCoroutines();
        _waveActive = false;
    }

    private IEnumerator SpawnWaveRoutine(Wave wave, int index)
    {
        _waveActive = true;
        _spawning = true;
        _aliveCount = 0;
        _spawnPositions.Clear();
        _aliveEnemies.Clear();
        onWaveStarted.Invoke(index);

        if (maxWaveDuration > 0f)
        {
            if (_timeoutRoutine != null) StopCoroutine(_timeoutRoutine);
            _timeoutRoutine = StartCoroutine(WaveTimeoutRoutine());
        }

        if (wave.enemyPrefab == null || wave.spawnWall == null)
        {
            Debug.LogWarning($"[WaveManager] Wave {index}: enemyPrefab o spawnWall mancante.");
            _spawning = false;
            _waveActive = false;
            yield break;
        }

        if (!_pools.TryGetValue(wave.enemyPrefab, out SimplePool pool))
        {
            pool = new SimplePool(wave.enemyPrefab, transform);
            _pools.Add(wave.enemyPrefab, pool);
        }

        for (int i = 0; i < wave.count; i++)
        {
            Vector3 pos = PickSpawnPosition(wave);
            _spawnPositions.Add(pos);

            GameObject enemy = pool.Get(pos, wave.spawnWall.rotation);
            _ownerPool[enemy] = pool;
            _aliveEnemies.Add(enemy);
            _aliveCount++;

            if (enemy.TryGetComponent(out Health health))
            {
                health.ResetHealth();
                if (!_wired.Contains(enemy))
                {
                    _wired.Add(enemy);
                    GameObject captured = enemy;
                    health.Died += () => OnEnemyDied(captured);
                }
            }

            if (enemy.TryGetComponent(out EnemyAI ai))
                ai.Configure();

            if (wave.spawnInterval > 0f)
                yield return new WaitForSeconds(wave.spawnInterval);
        }

        _spawning = false;

        // Se il Player ha già ripulito tutto durante lo spawn.
        if (_aliveCount <= 0 && _waveActive)
            WaveCleared();
    }

    // Posizione casuale ampia lungo (right) e verso l'arena (forward), tenuta
    // ad almeno minSpawnSeparation dai nemici già piazzati in questa wave.
    private Vector3 PickSpawnPosition(Wave wave)
    {
        Vector3 pos = Vector3.zero;
        int tries = Mathf.Max(1, spawnPlacementTries);

        for (int t = 0; t < tries; t++)
        {
            Vector3 lateral = wave.spawnWall.right * Random.Range(-wave.spawnSpread, wave.spawnSpread);
            Vector3 depth = wave.spawnWall.forward * Random.Range(wave.spawnMinDepth, wave.spawnDepth);
            pos = wave.spawnWall.position + lateral + depth;

            bool tooClose = false;
            for (int j = 0; j < _spawnPositions.Count; j++)
            {
                if ((_spawnPositions[j] - pos).sqrMagnitude < minSpawnSeparation * minSpawnSeparation)
                {
                    tooClose = true;
                    break;
                }
            }
            if (!tooClose) break;
        }

        return pos;
    }

    private void OnEnemyDied(GameObject enemy)
    {
        _aliveCount--;
        _aliveEnemies.Remove(enemy);
        EnemyKilled?.Invoke();

        // Un solo tiro: se va a segno, si droppa UN pickup e se ne sceglie il tipo.
        if (Random.value < healthDropChance)
        {
            GameObject prefab = ChooseDropPrefab();
            if (prefab != null)
            {
                // Y fissa: le anim di morte di alcuni nemici li portano sotto al
                // pavimento e il pickup spawnerebbe interrato.
                Vector3 dropPos = enemy.transform.position;
                dropPos.y = healthDropHeightY;
                SpawnHealthDrop(prefab, dropPos);
            }
        }

        StartCoroutine(ReleaseAfter(enemy, corpseDelay));

        if (_aliveCount <= 0 && _waveActive && !_spawning)
            WaveCleared();
    }

    // Sceglie il tipo del singolo drop: se sono assegnati entrambi i prefab,
    // tira a sorte (jammoDropShare); altrimenti usa l'unico disponibile.
    private GameObject ChooseDropPrefab()
    {
        bool hasPlayer = healthPickupPrefab != null;
        bool hasJammo = jammoHealthPickupPrefab != null;

        if (hasPlayer && hasJammo)
            return Random.value < jammoDropShare ? jammoHealthPickupPrefab : healthPickupPrefab;
        if (hasJammo) return jammoHealthPickupPrefab;
        return healthPickupPrefab; // null se nemmeno questo è assegnato
    }

    // Pool lazy per prefab pickup (uno per tipo: Player / Jammo). Il pickup si
    // auto-rilascia al suo pool tramite la callback Configure (HealthPickup).
    private void SpawnHealthDrop(GameObject prefab, Vector3 pos)
    {
        if (!_pickupPools.TryGetValue(prefab, out SimplePool pool))
        {
            pool = new SimplePool(prefab, transform);
            _pickupPools.Add(prefab, pool);
        }

        GameObject drop = pool.Get(pos, Quaternion.identity);
        if (drop.TryGetComponent(out HealthPickup pickup))
            pickup.Configure(() => pool.Release(drop));
    }

    // Rete di sicurezza anti-softlock: la wave non si è chiusa in tempo (es. un
    // nemico irraggiungibile). Rimette i superstiti nel pool e chiude la wave.
    private IEnumerator WaveTimeoutRoutine()
    {
        yield return new WaitForSeconds(maxWaveDuration);
        if (!_waveActive || _ended) yield break;

        for (int i = _aliveEnemies.Count - 1; i >= 0; i--)
        {
            GameObject e = _aliveEnemies[i];
            if (e != null && _ownerPool.TryGetValue(e, out SimplePool pool))
                pool.Release(e); // verrà resettato al prossimo spawn
        }
        _aliveEnemies.Clear();
        _aliveCount = 0;
        Debug.LogWarning("[WaveManager] Wave non ripulita entro maxWaveDuration: chiusura forzata (anti-softlock).", this);
        WaveCleared();
    }

    private IEnumerator ReleaseAfter(GameObject enemy, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (_ownerPool.TryGetValue(enemy, out SimplePool pool))
            pool.Release(enemy);
    }

    private void WaveCleared()
    {
        if (_timeoutRoutine != null) { StopCoroutine(_timeoutRoutine); _timeoutRoutine = null; }
        _waveActive = false;
        onWaveCleared.Invoke(_currentWaveIndex);
        if (_ended) return;

        bool lastWave = _currentWaveIndex >= waves.Length - 1;
        if (lastWave && !loopWaves)
        {
            onAllWavesCleared.Invoke();
            if (autoAdvance) StartCoroutine(AutoAdvanceRoutine()); // (no-op: StartNextWave si ferma)
            return;
        }

        // Loop / auto-advance: la prossima wave parte da sé dopo la pausa.
        if (loopWaves || autoAdvance) StartCoroutine(AutoAdvanceRoutine());
    }

    private IEnumerator AutoAdvanceRoutine()
    {
        if (delayBetweenWaves > 0f) yield return new WaitForSeconds(delayBetweenWaves);
        StartNextWave();
    }
}
