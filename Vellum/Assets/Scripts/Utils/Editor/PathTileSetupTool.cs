#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Editor menu tool that wires each <see cref="PathTile"/>'s 'solidCollider' field to its
/// single non-trigger BoxCollider in the open scene. Idempotent: skips tiles already linked
/// and flags anomalies (missing/multiple colliders). Registers a single Undo group.
/// </summary>
public static class PathTileSetupTool
{
    private const string MENU_PATH = "Tools/Vellum/Setup PathTile Solid Colliders";

    [MenuItem(MENU_PATH)]
    private static void LinkSolidColliders()
    {
        PathTile[] tiles = Object.FindObjectsByType<PathTile>(FindObjectsSortMode.None);
        if (tiles.Length == 0)
        {
            Debug.LogWarning("[PathTileSetupTool] No PathTile in the scene. Open the puzzle scene and run again.");
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

            // Collect ONLY the non-trigger BoxColliders on the tile's GameObject.
            // No AddComponent: the scene already has the physical floor for each tile.
            List<BoxCollider> nonTriggerColliders = new List<BoxCollider>();
            foreach (BoxCollider bc in tile.GetComponents<BoxCollider>())
            {
                if (!bc.isTrigger) nonTriggerColliders.Add(bc);
            }

            SerializedObject so = new SerializedObject(tile);
            SerializedProperty prop = so.FindProperty("solidCollider");
            if (prop == null)
            {
                Debug.LogError($"[PathTileSetupTool] {tile.name}: field 'solidCollider' not found on the PathTile script.", tile);
                skippedAnomaly++;
                continue;
            }

            // Idempotency: already linked to a valid non-trigger collider on the same GO?
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
                Debug.LogWarning($"[PathTileSetupTool] {tile.name}: no non-trigger BoxCollider found. Add one and run again.", tile);
                skippedAnomaly++;
                continue;
            }
            if (nonTriggerColliders.Count > 1)
            {
                Debug.LogWarning($"[PathTileSetupTool] {tile.name}: found {nonTriggerColliders.Count} non-trigger BoxColliders. Not auto-assigning — fix it by hand.", tile);
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
        Debug.Log($"[PathTileSetupTool] Linked {linked} tiles, skipped {skippedAlreadyOk} already OK, {skippedAnomaly} with anomalies.");
    }
}
#endif
