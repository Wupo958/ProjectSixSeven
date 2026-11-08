using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Defect), true)]
public sealed class DefectEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
 
        var defect = (Defect)target;
 
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Place this on a damage point of a WagonHalf prefab (one listed in its Damage Points). " +
                "Break/Repair test buttons appear here in Play Mode.", MessageType.Info);
            return;
        }
 
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Runtime", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Wagon / Point", $"{defect.WagonIndex} / {defect.PointIndex}");
        EditorGUILayout.LabelField("State", defect.IsBroken ? "ACTIVE (broken)" : "OK");
 
        TrainHealth train = TrainHealth.Instance;
        bool canAct = train != null && train.IsServer && defect.WagonIndex >= 0;
 
        using (new EditorGUI.DisabledScope(!canAct))
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Break")) train.ForceBreak(defect.WagonIndex, defect.PointIndex);
            if (GUILayout.Button("Repair")) train.RepairDefect(defect.WagonIndex, defect.PointIndex);
            if (GUILayout.Button("Destroy whole wagon")) train.ForceDestroyWagon(defect.WagonIndex);
            EditorGUILayout.EndHorizontal();
        }
 
        if (!canAct) EditorGUILayout.HelpBox("Only the host can break or repair.", MessageType.None);
 
        Repaint();
    }
}
