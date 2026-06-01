using UnityEngine;

// Modello 2. Il "pezzo" scala-1 è un'istanza INTERA del Jammo riggato (rig
// completo, così la mesh skinnata rende), di cui si mostra una sola parte via
// JammoPartSet. Pooled: niente Destroy (CLAUDE.md §4.3).
public class PieceSpawner : MonoBehaviour
{
    [Header("Sorgente pezzo")]
    [Tooltip("Prefab Variant di Jammo_Player a scala 1 con JammoPartSet.")]
    [SerializeField] private GameObject scale1JammoPrefab;

    [Header("Spawn")]
    [Tooltip("Punti candidati nell'arena (sul pavimento): ne viene scelto uno a caso. La Y del punto è rispettata.")]
    [SerializeField] private Transform[] spawnPoints;

    [Tooltip("Istanze pre-allocate per evitare hitch al primo spawn.")]
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

    public GameObject SpawnPiece(string partName)
    {
        if (scale1JammoPrefab == null || string.IsNullOrEmpty(partName)) return null;

        Vector3 pos = (spawnPoints != null && spawnPoints.Length > 0)
            ? spawnPoints[Random.Range(0, spawnPoints.Length)].position
            : transform.position;

        GameObject go = _pool.Get(pos, Quaternion.identity);
        go.transform.localScale = Vector3.one; // ReleasePiece lo rimpicciolisce a 0

        if (go.TryGetComponent(out JammoPartSet partSet))
            partSet.ShowOnly(partName);
        else
            Debug.LogWarning("[PieceSpawner] Il prefab scala-1 non ha JammoPartSet.", this);

        return go;
    }

    public void Release(GameObject piece) => _pool.Release(piece);
}
