using UnityEngine;

public class TileGridGenerator : MonoBehaviour
{
    [Header("Impostazioni")]
    [Tooltip("Trascina qui il Prefab della tua PuzzleTile")]
    public GameObject tilePrefab;
    
    [Tooltip("Trascina qui il tuo Plane (il pavimento da coprire)")]
    public Transform floorPlane;

    [Tooltip("La dimensione della singola tile (Se la tua tile ha Scale X=2 e Z=2, scrivi 2)")]
    public float tileSize = 2f;

    [Header("Anello Decorativo")]
    [Tooltip("Prefab della tile decorativa: solo mesh+materiale, niente PathTile/collider. Verra' generato attorno al puzzle.")]
    public GameObject decorativeTilePrefab;

    [Tooltip("Numero di anelli concentrici di tile decorative attorno alla griglia del puzzle.")]
    public int decorativeRings = 30;

    // Questa parolina magica "[ContextMenu]" ci permette di avviare il codice cliccando col tasto destro sullo script!
    [ContextMenu("Genera Griglia di Tile!")]
    public void GenerateGrid()
    {
        if (tilePrefab == null || floorPlane == null)
        {
            Debug.LogError("Attenzione: Manca il Prefab o il Plane di riferimento!");
            return;
        }

        // 1. Calcoliamo la grandezza in metri del tuo Plane.
        // Un Plane standard in Unity con Scale 1 è grande 10x10 metri. 
        // Con Scale 10x10, sarà grande 100x100 metri.
        float planeWidth = floorPlane.localScale.x * 6f;
        float planeLength = floorPlane.localScale.z * 6f; // Z è la profondità per i pavimenti

        // Calcoliamo quante righe e colonne ci stanno
        int columns = Mathf.RoundToInt(planeWidth / tileSize);
        int rows = Mathf.RoundToInt(planeLength / tileSize);

        // 2. Calcoliamo il punto d'angolo in alto a sinistra da cui iniziare a stampare le tile
        Vector3 startPos = floorPlane.position - new Vector3(planeWidth / 2f, 0f, planeLength / 2f);
        // Spostiamo il centro per allinearlo al centro della prima tile
        startPos += new Vector3(tileSize / 2f, 0f, tileSize / 2f);

        // 3. Creiamo una cartella "Contenitore" nella Hierarchy per non fare confusione con migliaia di oggetti
        GameObject gridContainer = new GameObject("TileGrid_Container");
        gridContainer.transform.position = floorPlane.position;

        // 4. Creiamo la griglia con un doppio ciclo!
        for (int x = 0; x < columns; x++)
        {
            for (int z = 0; z < rows; z++)
            {
                // Calcoliamo la posizione esatta della singola tile
                Vector3 spawnPosition = startPos + new Vector3(x * tileSize, floorPlane.position.y, z * tileSize);

                // Instanziamo la tile
                GameObject newTile = Instantiate(tilePrefab, spawnPosition, Quaternion.identity);
                
                // Mettiamola nel contenitore e diamole un nome ordinato
                newTile.transform.SetParent(gridContainer.transform);
                newTile.name = $"Tile_{x}_{z}";
            }
        }

        Debug.Log($"Fatto! Ho generato {columns * rows} Tile.");
    }

    [ContextMenu("Genera Anello Decorativo!")]
    public void GenerateDecorativeRing()
    {
        if (decorativeTilePrefab == null || floorPlane == null)
        {
            Debug.LogError("Attenzione: Manca il Prefab decorativo o il Plane di riferimento!");
            return;
        }

        if (decorativeRings <= 0)
        {
            Debug.LogWarning("decorativeRings <= 0: nessuna tile da generare.");
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
                // 1. Salta la zona interna del puzzle
                bool insidePuzzle = x >= innerXMin && x < innerXMax && z >= innerZMin && z < innerZMax;
                if (insidePuzzle) continue;

                // --- INIZIO MAGIA DEGLI ANGOLI ARROTONDATI ---
                // 2. Calcoliamo di quanti "passi" siamo lontani dal puzzle sui due assi
                float dx = 0;
                if (x < innerXMin) dx = innerXMin - x;
                else if (x >= innerXMax) dx = x - (innerXMax - 1);

                float dz = 0;
                if (z < innerZMin) dz = innerZMin - z;
                else if (z >= innerZMax) dz = z - (innerZMax - 1);

                // 3. Teorema di Pitagora: calcoliamo la distanza reale dall'angolo del puzzle!
                float distance = Mathf.Sqrt(dx * dx + dz * dz);

                // 4. Se la distanza è maggiore dei nostri anelli, tagliamo la tile!
                // Aggiungiamo 0.5f per far venire la curva più morbida e non "mangiare" troppo bordo.
                if (distance > decorativeRings + 0.5f) continue;
                // --- FINE MAGIA ---

                Vector3 spawnPosition = startPos + new Vector3(x * tileSize, floorPlane.position.y, z * tileSize);

                GameObject newTile = Instantiate(decorativeTilePrefab, spawnPosition, Quaternion.identity);
                newTile.transform.SetParent(ringContainer.transform);
                newTile.name = $"Deco_{x}_{z}";

                newTile.isStatic = true;
                count++;
            }
        }

        Debug.Log($"Generato anello decorativo ARROTONDATO con {count} tile.");
    }
}