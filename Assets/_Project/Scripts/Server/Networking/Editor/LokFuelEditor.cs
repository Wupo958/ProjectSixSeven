using UnityEditor;
using UnityEngine;
 
[CustomEditor(typeof(LokFuel))]
public sealed class LokFuelEditor : Editor
{
    public override bool RequiresConstantRepaint() => Application.isPlaying;
 
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
 
        var tank = (LokFuel)target;
        bool live = Application.isPlaying && tank.IsSpawned;
 
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Testing", EditorStyles.boldLabel);
 
        if (!live)
        {
            EditorGUILayout.HelpBox("The buttons work in Play mode.", MessageType.Info);
            return;
        }
 
        EditorGUILayout.LabelField("Fuel", $"{tank.Fuel:0.0} / {tank.Capacity:0}  ({tank.Fraction * 100f:0}%)");
 
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Empty")) tank.RequestSetFuel(0f);
        if (GUILayout.Button("+1 Item")) tank.RequestRefuel();
        if (GUILayout.Button("Fill")) tank.RequestSetFuel(tank.Capacity);
        EditorGUILayout.EndHorizontal();
    }
}