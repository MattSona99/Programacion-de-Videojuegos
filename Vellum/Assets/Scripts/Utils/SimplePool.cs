using System.Collections.Generic;
using UnityEngine;

// Pool riusabile di istanze di un prefab. Niente Destroy: gli oggetti rilasciati
// vengono disattivati e riusati (convenzione CLAUDE.md per i nemici).
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

    public GameObject Get(Vector3 position, Quaternion rotation)
    {
        GameObject go = _free.Count > 0 ? _free.Dequeue() : Object.Instantiate(_prefab, _parent);
        go.transform.SetPositionAndRotation(position, rotation);
        go.SetActive(true);
        return go;
    }

    public void Release(GameObject go)
    {
        if (go == null) return;
        go.SetActive(false);
        _free.Enqueue(go);
    }
}
