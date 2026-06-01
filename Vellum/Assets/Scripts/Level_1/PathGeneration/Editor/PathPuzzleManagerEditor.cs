using UnityEditor;
using UnityEngine;

/// <summary>Custom inspector for <see cref="PathPuzzleManager"/> adding a "Generate Path!" button to preview a path in the editor.</summary>
[CustomEditor(typeof(PathPuzzleManager))]
public class PathPuzzleManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        Color prev = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.55f, 0.85f, 0.55f);
        if (GUILayout.Button("Generate Path!", GUILayout.Height(32)))
        {
            PathPuzzleManager mgr = (PathPuzzleManager)target;
            Undo.RecordObject(mgr, "Generate Path");
            mgr.GenerateRandomPath();
            EditorUtility.SetDirty(mgr);
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = prev;
    }
}
