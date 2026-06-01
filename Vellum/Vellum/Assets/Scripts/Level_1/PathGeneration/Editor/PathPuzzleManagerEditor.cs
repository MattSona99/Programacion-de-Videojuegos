using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PathPuzzleManager))]
public class PathPuzzleManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        Color prev = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.55f, 0.85f, 0.55f);
        if (GUILayout.Button("Genera Percorso!", GUILayout.Height(32)))
        {
            PathPuzzleManager mgr = (PathPuzzleManager)target;
            Undo.RecordObject(mgr, "Genera Percorso");
            mgr.GenerateRandomPath();
            EditorUtility.SetDirty(mgr);
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = prev;
    }
}
