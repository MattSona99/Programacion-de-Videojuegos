#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PathTileSetupTool
{
    private const string MENU_PATH = "Tools/Vellum/Setup PathTile Solid Colliders";

    [MenuItem(MENU_PATH)]
    private static void LinkSolidColliders()
    {
        PathTile[] tiles = Object.FindObjectsByType<PathTile>(FindObjectsSortMode.None);
        if (tiles.Length == 0)
        {
            Debug.LogWarning("[PathTileSetupTool] Nessuna PathTile in scena. Apri la scena con il puzzle e rilancia.");
            return;
        }

        int linked = 0;
        int skippedAlreadyOk = 0;
        int skippedAnomaly = 0;

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Link PathTile solid colliders");
        int undoGroup = Undo.GetCurrentGroup();

        foreach (PathTile tile in tiles)
        {
            if (tile == null) continue;

            // Raccogliamo SOLO i BoxCollider non-trigger presenti sul GameObject della tile.
            // Niente AddComponent: la scena ha già il pavimento fisico per ogni tile.
            List<BoxCollider> nonTriggerColliders = new List<BoxCollider>();
            foreach (BoxCollider bc in tile.GetComponents<BoxCollider>())
            {
                if (!bc.isTrigger) nonTriggerColliders.Add(bc);
            }

            SerializedObject so = new SerializedObject(tile);
            SerializedProperty prop = so.FindProperty("solidCollider");
            if (prop == null)
            {
                Debug.LogError($"[PathTileSetupTool] {tile.name}: campo 'solidCollider' non trovato sullo script PathTile.", tile);
                skippedAnomaly++;
                continue;
            }

            // Idempotenza: già linkato a un collider non-trigger valido sullo stesso GO?
            Object current = prop.objectReferenceValue;
            if (current is Collider currentCollider
                && currentCollider != null
                && currentCollider.gameObject == tile.gameObject
                && !currentCollider.isTrigger)
            {
                skippedAlreadyOk++;
                continue;
            }

            if (nonTriggerColliders.Count == 0)
            {
                Debug.LogWarning($"[PathTileSetupTool] {tile.name}: nessun BoxCollider non-trigger trovato. Aggiungine uno e rilancia.", tile);
                skippedAnomaly++;
                continue;
            }
            if (nonTriggerColliders.Count > 1)
            {
                Debug.LogWarning($"[PathTileSetupTool] {tile.name}: trovati {nonTriggerColliders.Count} BoxCollider non-trigger. Non assegno automaticamente — sistema a mano.", tile);
                skippedAnomaly++;
                continue;
            }

            Undo.RecordObject(tile, "Link PathTile solid collider");
            prop.objectReferenceValue = nonTriggerColliders[0];
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(tile);
            linked++;
        }

        if (linked > 0)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        Undo.CollapseUndoOperations(undoGroup);
        Debug.Log($"[PathTileSetupTool] Collegate {linked} tile, skippate {skippedAlreadyOk} già a posto, {skippedAnomaly} con anomalie.");
    }
}
#endif
