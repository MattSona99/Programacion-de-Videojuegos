using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reusable pool of prefab instances. No Destroy: released objects are deactivated
/// and reused (per the CLAUDE.md convention for enemies).
/// </summary>
public class SimplePool
{
    private readonly GameObject _prefab;
    private readonly Transform _parent;
    private readonly Queue<GameObject> _free = new Queue<GameObject>();

    public SimplePool(GameObject prefab, Transform parent = null)
    {
        _prefab = prefab;
        _parent = parent;
    }

    /// <summary>Returns a pooled instance at the given pose (reusing a free one, or instantiating).</summary>
    public GameObject Get(Vector3 position, Quaternion rotation)
    {
        GameObject go = _free.Count > 0 ? _free.Dequeue() : Object.Instantiate(_prefab, _parent);
        go.transform.SetPositionAndRotation(position, rotation);
        go.SetActive(true);
        return go;
    }

    /// <summary>Deactivates the instance and returns it to the pool for reuse.</summary>
    public void Release(GameObject go)
    {
        if (go == null) return;
        go.SetActive(false);
        _free.Enqueue(go);
    }
}
