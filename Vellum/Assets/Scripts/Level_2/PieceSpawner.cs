using UnityEngine;

/// <summary>
/// Spawns the carried "pieces" of Act 2. Each scale-1 "piece" is a WHOLE instance of the rigged
/// Jammo (full rig, so the skinned mesh renders), with only one part shown via JammoPartSet.
/// Pooled: no Destroy (CLAUDE.md §4.3).
/// </summary>
public class PieceSpawner : MonoBehaviour
{
    [Header("Piece source")]
    [Tooltip("Variant prefab of Jammo_Player at scale 1 with JammoPartSet.")]
    [SerializeField] private GameObject scale1JammoPrefab;

    [Header("Spawn")]
    [Tooltip("Candidate points in the arena (on the floor): one is chosen at random. The point's Y is respected.")]
    [SerializeField] private Transform[] spawnPoints;

    [Tooltip("Pre-allocated instances to avoid a hitch on the first spawn.")]
    [SerializeField] private int poolSize = 2;

    private SimplePool _pool;

    void Awake()
    {
        _pool = new SimplePool(scale1JammoPrefab, transform);

        if (poolSize > 0 && scale1JammoPrefab != null)
        {
            var warm = new GameObject[poolSize];
            for (int i = 0; i < poolSize; i++)
                warm[i] = _pool.Get(transform.position, Quaternion.identity);
            for (int i = 0; i < poolSize; i++)
                _pool.Release(warm[i]);
        }
    }

    /// <summary>Spawns a pooled piece at a random spawn point, showing only <paramref name="partName"/>.</summary>
    public GameObject SpawnPiece(string partName)
    {
        if (scale1JammoPrefab == null || string.IsNullOrEmpty(partName)) return null;

        Vector3 pos = (spawnPoints != null && spawnPoints.Length > 0)
            ? spawnPoints[Random.Range(0, spawnPoints.Length)].position
            : transform.position;

        GameObject go = _pool.Get(pos, Quaternion.identity);
        go.transform.localScale = Vector3.one; // the carrier shrinks it back to 0 on release

        if (go.TryGetComponent(out JammoPartSet partSet))
            partSet.ShowOnly(partName);
        else
            Debug.LogWarning("[PieceSpawner] The scale-1 prefab has no JammoPartSet.", this);

        return go;
    }

    /// <summary>Returns a piece to the pool.</summary>
    public void Release(GameObject piece) => _pool.Release(piece);
}
