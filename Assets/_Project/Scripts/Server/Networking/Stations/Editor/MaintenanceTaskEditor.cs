using UnityEditor;
using UnityEngine;
 
[CustomEditor(typeof(MaintenanceTask), true)]
public sealed class MaintenanceTaskEditor : Editor
{
    public override bool RequiresConstantRepaint() => Application.isPlaying;
 
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
 
        var task = (MaintenanceTask)target;
 
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Testing", EditorStyles.boldLabel);
 
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Works in Play mode on a task of the built train " +
                                    "(train root > Generated > ...).", MessageType.Info);
            return;
        }
 
        EditorGUILayout.LabelField("State", task.IsActive ? $"DUE - strength {task.Severity * 100f:0}%" : "OK");
        if (task.IsLockedByOther) EditorGUILayout.LabelField("In use by another player");
 
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Make Due Now")) task.DebugSetActive(true);
        if (GUILayout.Button("Clear")) task.DebugSetActive(false);
        EditorGUILayout.EndHorizontal();
    }
}