using UnityEditor;
using UnityEngine;
 
[CustomEditor(typeof(WeaponBase), true)]
public sealed class WeaponBaseEditor : Editor
{
    public override bool RequiresConstantRepaint() => Application.isPlaying;
 
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
 
        var weapon = (WeaponBase)target;
 
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Wear & faults (testing)", EditorStyles.boldLabel);
 
        if (!Application.isPlaying || !weapon.IsSpawned)
        {
            EditorGUILayout.HelpBox("Works in Play mode on a weapon of the built train.", MessageType.Info);
            return;
        }
 
        EditorGUILayout.LabelField("Wear", weapon.IsJammed ? "100% - FAILED" : $"{weapon.Wear * 100f:0}%");
 
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+25% Wear")) weapon.DebugAddWear(0.25f);
        if (GUILayout.Button("Maintain")) weapon.RequestMaintenance();
        EditorGUILayout.EndHorizontal();
 
        WeaponFault faults = weapon.Faults;
        WeaponFault edited = (WeaponFault)EditorGUILayout.EnumFlagsField("Faults", faults);
        if (edited != faults) weapon.DebugSetFaults(edited);
    }
}