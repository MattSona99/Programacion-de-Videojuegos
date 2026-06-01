using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>UnityEvent carrying a wave index.</summary>
[System.Serializable]
public class WaveIndexEvent : UnityEvent<int> { }

/// <summary>
/// Wave engine for Act_02. Externally driven: the director (dialogues/camera/pauses) calls
/// StartNextWave() after its beats. The assembly director hooks onWaveCleared / EnemyKilled for
/// the piece drop / Jammo logic. Pooled enemies, separated spawns, and optional looping/timeout.
/// </summary>
public class WaveManager : MonoBehaviour
{
    /// <summary>Configuration of a single wave: spawn wall, enemy prefab, count, and spawn distribution.</summary>
    [System.Serializable]
    public class Wave
    {
        [Tooltip("Transform of the wall this wave spawns from (oriented toward the arena).")]
        public Transform spawnWall;
        public GameObject enemyPrefab;
        public int count = 5;
        [Tooltip("Seconds between one spawn and the next within the wave.")]
        public float spawnInterval = 0.5f;
        [Tooltip("Lateral spread along the wall (Transform's right axis).")]
        public float spawnSpread = 6f;
        [Tooltip("Minimum spawn depth toward the arena: keeps enemies off the wall, never flush against it.")]
        public float spawnMinDepth = 2f;
        [Tooltip("Spawn depth toward the arena (Transform's forward axis).")]
        public float spawnDepth = 4f;
    }

    [Header("Waves (one wall per wave)")]
    [SerializeField] private Wave[] waves;

    [Header("Spawn distribution")]
    [Tooltip("Minimum distance between two enemies of the same wave (anti-overlap).")]
    [SerializeField] private float minSpawnSeparation = 1.5f;
    [Tooltip("Re-placement attempts before accepting the position anyway.")]
    [SerializeField] private int spawnPlacementTries = 10;

    [Header("Pooling")]
    [Tooltip("Seconds before returning the corpse to the pool (time for the death animation).")]
    [SerializeField] private float corpseDelay = 1.5f;

    [Header("Health drops")]
    [Tooltip("HealthPickup prefab (heals the Player) spawned when an enemy dies. Leave empty to disable drops.")]
    [SerializeField] private GameObject healthPickupPrefab;
    [Tooltip("HealthPickup prefab configured for Jammo (+25 to Jammo, different color). Same spawn chance as the Player pickup. Leave empty to disable.")]
    [SerializeField] private GameObject jammoHealthPickupPrefab;
    [Tooltip("Probability 0..1 that a killed enemy drops ONE pickup (then its type is chosen: Player or Jammo).")]
    [SerializeField, Range(0f, 1f)] private float healthDropChance = 0.25f;
    [Tooltip("When a drop happens and both prefabs are assigned, probability 0..1 that it's the JAMMO pickup (the rest is the Player pickup). 0.5 = 50/50.")]
    [SerializeField, Range(0f, 1f)] private float jammoDropShare = 0.5f;
    [Tooltip("Constant Y height the pickup spawns at, regardless of the corpse's pose. Avoids drops inside the floor when death anims lower the enemy.")]
    [SerializeField] private float healthDropHeightY = 1f;

    [Header("Wave loop")]
    [Tooltip("Waves repeat in a loop (index % wave count). The arena ends when the statue is complete, not by wave count.")]
    [SerializeField] private bool loopWaves = true;
    [Tooltip("Start the first wave by itself on Play (together with Jammo).")]
    [SerializeField] private bool startOnPlay = true;
    [Tooltip("Auto-advance to the next wave (besides looping). Leave OFF if an external orchestrator drives it and loop is OFF.")]
    [SerializeField] private bool autoAdvance = false;
    [Tooltip("Pause between one cleared wave and the next.")]
    [SerializeField] private float delayBetweenWaves = 2f;
    [Tooltip("Safety net: if a wave isn't cleared within N seconds, the remaining enemies are returned to the pool and the wave closes (anti-softlock). 0 = disabled.")]
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

    /// <summary>Fired on every killed enemy: the statue director hooks here to queue a drop.</summary>
    public event System.Action EnemyKilled;

    void Start()
    {
        if (startOnPlay) StartNextWave();
    }

    /// <summary>Starts the next wave. One at a time (_waveActive gate). When looping, the index wraps on % waves.Length; only stops at arena end (_ended).</summary>
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

    /// <summary>Called by the director when the statue is complete: definitive stop.</summary>
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
            Debug.LogWarning($"[WaveManager] Wave {index}: enemyPrefab or spawnWall missing.");
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

        // If the Player already cleared everything during the spawn.
        if (_aliveCount <= 0 && _waveActive)
            WaveCleared();
    }

    /// <summary>Random position spread along (right) and toward the arena (forward), kept at least minSpawnSeparation from already-placed enemies of this wave.</summary>
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

        // A single roll: if it succeeds, ONE pickup is dropped and its type is chosen.
        if (Random.value < healthDropChance)
        {
            GameObject prefab = ChooseDropPrefab();
            if (prefab != null)
            {
                // Fixed Y: some enemies' death anims take them under the floor and the
                // pickup would spawn buried.
                Vector3 dropPos = enemy.transform.position;
                dropPos.y = healthDropHeightY;
                SpawnHealthDrop(prefab, dropPos);
            }
        }

        StartCoroutine(ReleaseAfter(enemy, corpseDelay));

        if (_aliveCount <= 0 && _waveActive && !_spawning)
            WaveCleared();
    }

    /// <summary>Chooses the single drop's type: if both prefabs are assigned, rolls (jammoDropShare); otherwise uses the only one available.</summary>
    private GameObject ChooseDropPrefab()
    {
        bool hasPlayer = healthPickupPrefab != null;
        bool hasJammo = jammoHealthPickupPrefab != null;

        if (hasPlayer && hasJammo)
            return Random.value < jammoDropShare ? jammoHealthPickupPrefab : healthPickupPrefab;
        if (hasJammo) return jammoHealthPickupPrefab;
        return healthPickupPrefab; // null if this one isn't assigned either
    }

    /// <summary>Lazy pool per pickup prefab (one per type: Player / Jammo). The pickup auto-releases to its pool via the Configure callback (HealthPickup).</summary>
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

    /// <summary>Anti-softlock safety net: the wave didn't close in time (e.g. an unreachable enemy). Returns the survivors to the pool and closes the wave.</summary>
    private IEnumerator WaveTimeoutRoutine()
    {
        yield return new WaitForSeconds(maxWaveDuration);
        if (!_waveActive || _ended) yield break;

        for (int i = _aliveEnemies.Count - 1; i >= 0; i--)
        {
            GameObject e = _aliveEnemies[i];
            if (e != null && _ownerPool.TryGetValue(e, out SimplePool pool))
                pool.Release(e); // will be reset on the next spawn
        }
        _aliveEnemies.Clear();
        _aliveCount = 0;
        Debug.LogWarning("[WaveManager] Wave not cleared within maxWaveDuration: forced close (anti-softlock).", this);
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
            if (autoAdvance) StartCoroutine(AutoAdvanceRoutine()); // (no-op: StartNextWave stops)
            return;
        }

        // Loop / auto-advance: the next wave starts by itself after the pause.
        if (loopWaves || autoAdvance) StartCoroutine(AutoAdvanceRoutine());
    }

    private IEnumerator AutoAdvanceRoutine()
    {
        if (delayBetweenWaves > 0f) yield return new WaitForSeconds(delayBetweenWaves);
        StartNextWave();
    }
}
