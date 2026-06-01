using UnityEngine;
using UnityEngine.Serialization;
using UnityEditor;

/// <summary>
/// Editor utility that scatters tree prefabs in a ring around this object, snapping each
/// to the terrain via a downward raycast and randomizing rotation and scale. Run from the
/// component's context menu in the Inspector.
/// </summary>
public class ForestSower : MonoBehaviour
{
    [Header("Tree Assets")]
    [FormerlySerializedAs("prefabsAlberi")]
    public GameObject[] treePrefabs;

    [Header("Ring Settings")]
    [FormerlySerializedAs("raggioMinimo")]
    public float minRadius = 10f;
    [FormerlySerializedAs("raggioMassimo")]
    public float maxRadius = 25f;
    [FormerlySerializedAs("quantita")]
    public int count = 100;

    [Header("Variations")]
    [FormerlySerializedAs("scalaMinima")]
    public float minScale = 0.8f;
    [FormerlySerializedAs("scalaMassima")]
    public float maxScale = 1.5f;

    [Header("Terrain Filter")]
    [Tooltip("Set the terrain layer here so the raycast ignores trees and the player")]
    [FormerlySerializedAs("layerTerreno")]
    public LayerMask terrainLayer;

    /// <summary>Instantiates <see cref="count"/> tree prefabs in the ring, snapped to the terrain.</summary>
    [ContextMenu("Generate Permanent Forest")]
    public void GenerateForest()
    {
        if (treePrefabs.Length == 0) return;

        for (int i = 0; i < count; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2);
            float distance = Mathf.Sqrt(Random.Range(minRadius * minRadius, maxRadius * maxRadius));

            Vector3 basePosition = transform.position + new Vector3(
                Mathf.Cos(angle) * distance,
                0,
                Mathf.Sin(angle) * distance
            );

            Vector3 skyPoint = new Vector3(basePosition.x, 50f, basePosition.z);

            // The raycast now uses the terrain filter (terrainLayer)
            if (Physics.Raycast(skyPoint, Vector3.down, out RaycastHit hit, 100f, terrainLayer))
            {
                basePosition.y = hit.point.y;
            }
            else
            {
                // If the ray misses the terrain (e.g. off-map), skip this instance
                continue;
            }

            GameObject chosenTree = treePrefabs[Random.Range(0, treePrefabs.Length)];
            GameObject newTree = (GameObject)PrefabUtility.InstantiatePrefab(chosenTree);

            newTree.transform.position = basePosition;
            newTree.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
            newTree.transform.localScale = Vector3.one * Random.Range(minScale, maxScale);
            newTree.transform.parent = this.transform;
        }

        Debug.Log("Smart generation complete!");
    }

    /// <summary>Removes every generated child object.</summary>
    [ContextMenu("Clear All")]
    public void Clear()
    {
        while (transform.childCount > 0)
        {
            DestroyImmediate(transform.GetChild(0).gameObject);
        }
    }
}