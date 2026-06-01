using System.Collections.Generic;
using UnityEngine;

// Spawner dei pickup di vita NEUTRI del duello finale. A ogni colpo a segno
// (Player↔Boss) può comparire un pickup in un punto casuale dell'arena,
// raccoglibile da entrambi. Si aggancia ai due Health via l'evento C# solo-danno
// Health.Damaged (NON onDamaged, che scatta anche su Heal → eviterebbe il loop
// spawn→cura→spawn). Pool-friendly (SimplePool). Tiene il registro dei pickup
// attivi per la decisione fuzzy del boss (BossDuelAI).
public class DuelHealthSpawner : MonoBehaviour
{
    [Header("Contendenti")]
    [SerializeField] private Health playerHealth;
    [SerializeField] private Health bossHealth;

    [Header("Pickup")]
    [Tooltip("Prefab del DuelHealthPickup (Collider trigger + look in Editor).")]
    [SerializeField] private GameObject pickupPrefab;
    [SerializeField] private float healAmount = 35f;
    [Tooltip("Secondi dopo i quali un pickup non raccolto scompare.")]
    [SerializeField] private float pickupLifetime = 12f;
    [SerializeField] private int poolSize = 4;

    [Header("Spawn")]
    [Tooltip("Centro dell'arena: i pickup compaiono entro 'arenaRadius' su questo piano (XZ).")]
    [SerializeField] private Transform arenaCenter;
    [SerializeField] private float arenaRadius = 7f;
    [Tooltip("Offset verticale rispetto al piano di arenaCenter (per posarlo a terra).")]
    [SerializeField] private float spawnHeight = 0.5f;
    [Range(0f, 1f)]
    [Tooltip("Probabilità di drop a ogni colpo a segno.")]
    [SerializeField] private float dropChance = 0.3f;
    [Tooltip("Intervallo minimo tra due spawn (anti-flood).")]
    [SerializeField] private float minSpawnInterval = 4f;

    private SimplePool _pool;
    private readonly List<DuelHealthPickup> _active = new List<DuelHealthPickup>();
    private float _nextSpawnAllowed;

    void Awake()
    {
        if (pickupPrefab != null) _pool = new SimplePool(pickupPrefab, transform);
    }

    void OnEnable()
    {
        if (playerHealth != null) playerHealth.Damaged += OnCombatHit;
        if (bossHealth != null) bossHealth.Damaged += OnCombatHit;
    }

    void OnDisable()
    {
        if (playerHealth != null) playerHealth.Damaged -= OnCombatHit;
        if (bossHealth != null) bossHealth.Damaged -= OnCombatHit;
    }

    private void OnCombatHit(DamageInfo info)
    {
        if (_pool == null) return;
        if (Time.time < _nextSpawnAllowed) return;
        if (Random.value >= dropChance) return;

        _nextSpawnAllowed = Time.time + minSpawnInterval;
        Spawn();
    }

    private void Spawn()
    {
        Vector3 center = arenaCenter != null ? arenaCenter.position : transform.position;
        Vector2 disc = Random.insideUnitCircle * arenaRadius;
        Vector3 pos = new Vector3(center.x + disc.x, center.y + spawnHeight, center.z + disc.y);

        GameObject go = _pool.Get(pos, Quaternion.identity);
        if (!go.TryGetComponent(out DuelHealthPickup pickup))
        {
            _pool.Release(go);
            return;
        }

        pickup.Configure(() => Remove(pickup), playerHealth, bossHealth, healAmount, pickupLifetime);
        _active.Add(pickup);
    }

    private void Remove(DuelHealthPickup pickup)
    {
        _active.Remove(pickup);
        if (pickup != null) _pool.Release(pickup.gameObject);
    }

    // Pickup attivo più vicino a 'from' che sia "pronto" (vivo da almeno minAge:
    // il ritardo di reazione del boss). false se nessuno qualifica.
    public bool TryGetNearestReady(Vector3 from, float minAge, out Transform pickup, out float distance)
    {
        pickup = null;
        distance = Mathf.Infinity;
        float now = Time.time;

        for (int i = 0; i < _active.Count; i++)
        {
            DuelHealthPickup p = _active[i];
            if (p == null) continue;
            if (now - p.SpawnTime < minAge) continue;

            float d = Vector3.Distance(from, p.transform.position);
            if (d < distance)
            {
                distance = d;
                pickup = p.transform;
            }
        }
        return pickup != null;
    }
}
