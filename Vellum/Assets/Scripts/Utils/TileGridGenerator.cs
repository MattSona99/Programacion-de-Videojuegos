using UnityEngine;

/// <summary>
/// Editor utility that procedurally spawns the Act 1 puzzle tile grid over a floor Plane,
/// and an optional rounded decorative ring of mesh-only tiles around it. Triggered from
/// the component's context menu in the Inspector.
/// </summary>
public class TileGridGenerator : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Drag your PuzzleTile prefab here")]
    public GameObject tilePrefab;

    [Tooltip("Drag your Plane here (the floor to cover)")]
    public Transform floorPlane;

    [Tooltip("Size of a single tile (if your tile has Scale X=2 and Z=2, enter 2)")]
    public float tileSize = 2f;

    [Header("Decorative Ring")]
    [Tooltip("Decorative tile prefab: mesh+material only, no PathTile/collider. Generated around the puzzle.")]
    public GameObject decorativeTilePrefab;

    [Tooltip("Number of concentric rings of decorative tiles around the puzzle grid.")]
    public int decorativeRings = 30;

    /// <summary>Spawns the full grid of playable puzzle tiles over the floor Plane.</summary>
    // The "[ContextMenu]" attribute lets us run this by right-clicking the script in the Inspector.
    [ContextMenu("Generate Tile Grid!")]
    public void GenerateGrid()
    {
        if (tilePrefab == null || floorPlane == null)
        {
            Debug.LogError("Warning: missing the reference Prefab or Plane!");
            return;
        }

        // 1. Compute the Plane size in metres.
        // A standard Unity Plane at Scale 1 is 10x10 metres.
        // At Scale 10x10 it is 100x100 metres.
        float planeWidth = floorPlane.localScale.x * 6f;
        float planeLength = floorPlane.localScale.z * 6f; // Z is the depth for floors

        // Compute how many rows and columns fit
        int columns = Mathf.RoundToInt(planeWidth / tileSize);
        int rows = Mathf.RoundToInt(planeLength / tileSize);

        // 2. Compute the top-left corner point to start laying out the tiles from
        Vector3 startPos = floorPlane.position - new Vector3(planeWidth / 2f, 0f, planeLength / 2f);
        // Shift by half a tile to align with the centre of the first tile
        startPos += new Vector3(tileSize / 2f, 0f, tileSize / 2f);

        // 3. Create a container in the Hierarchy to avoid cluttering it with thousands of objects
        GameObject gridContainer = new GameObject("TileGrid_Container");
        gridContainer.transform.position = floorPlane.position;

        // 4. Build the grid with a double loop
        for (int x = 0; x < columns; x++)
        {
            for (int z = 0; z < rows; z++)
            {
                // Compute the exact position of the single tile
                Vector3 spawnPosition = startPos + new Vector3(x * tileSize, floorPlane.position.y, z * tileSize);

                // Instantiate the tile
                GameObject newTile = Instantiate(tilePrefab, spawnPosition, Quaternion.identity);

                // Put it in the container and give it an ordered name
                newTile.transform.SetParent(gridContainer.transform);
                newTile.name = $"Tile_{x}_{z}";
            }
        }

        Debug.Log($"Done! Generated {columns * rows} tiles.");
    }

    /// <summary>Spawns a rounded decorative ring of mesh-only tiles around the puzzle grid.</summary>
    [ContextMenu("Generate Decorative Ring!")]
    public void GenerateDecorativeRing()
    {
        if (decorativeTilePrefab == null || floorPlane == null)
        {
            Debug.LogError("Warning: missing the decorative Prefab or the reference Plane!");
            return;
        }

        if (decorativeRings <= 0)
        {
            Debug.LogWarning("decorativeRings <= 0: no tiles to generate.");
            return;
        }

        float planeWidth = floorPlane.localScale.x * 6f;
        float planeLength = floorPlane.localScale.z * 6f;

        int columns = Mathf.RoundToInt(planeWidth / tileSize);
        int rows = Mathf.RoundToInt(planeLength / tileSize);

        int outerColumns = columns + 2 * decorativeRings;
        int outerRows = rows + 2 * decorativeRings;

        Vector3 startPos = floorPlane.position
            - new Vector3(planeWidth / 2f, 0f, planeLength / 2f)
            - new Vector3(decorativeRings * tileSize, 0f, decorativeRings * tileSize);
        startPos += new Vector3(tileSize / 2f, 0f, tileSize / 2f);

        GameObject ringContainer = new GameObject("DecorativeRing_Container");
        ringContainer.transform.position = floorPlane.position;

        int innerXMin = decorativeRings;
        int innerXMax = decorativeRings + columns;
        int innerZMin = decorativeRings;
        int innerZMax = decorativeRings + rows;

        int count = 0;
        for (int x = 0; x < outerColumns; x++)
        {
            for (int z = 0; z < outerRows; z++)
            {
                // 1. Skip the inner puzzle area
                bool insidePuzzle = x >= innerXMin && x < innerXMax && z >= innerZMin && z < innerZMax;
                if (insidePuzzle) continue;

                // --- Rounded-corner logic ---
                // 2. Compute how many "steps" we are away from the puzzle on each axis
                float dx = 0;
                if (x < innerXMin) dx = innerXMin - x;
                else if (x >= innerXMax) dx = x - (innerXMax - 1);

                float dz = 0;
                if (z < innerZMin) dz = innerZMin - z;
                else if (z >= innerZMax) dz = z - (innerZMax - 1);

                // 3. Pythagoras: the real distance from the puzzle corner
                float distance = Mathf.Sqrt(dx * dx + dz * dz);

                // 4. If the distance exceeds our ring count, skip the tile.
                // Add 0.5f for a softer curve that doesn't eat too much of the border.
                if (distance > decorativeRings + 0.5f) continue;
                // --- end rounded-corner logic ---

                Vector3 spawnPosition = startPos + new Vector3(x * tileSize, floorPlane.position.y, z * tileSize);

                GameObject newTile = Instantiate(decorativeTilePrefab, spawnPosition, Quaternion.identity);
                newTile.transform.SetParent(ringContainer.transform);
                newTile.name = $"Deco_{x}_{z}";

                newTile.isStatic = true;
                count++;
            }
        }

        Debug.Log($"Generated rounded decorative ring with {count} tiles.");
    }
}