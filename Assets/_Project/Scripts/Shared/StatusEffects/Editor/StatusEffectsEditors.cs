using UnityEditor;
using UnityEngine;
 
[CustomEditor(typeof(StatusEffectReceiver))]
public sealed class StatusEffectReceiverEditor : Editor
{
    private int _selected;
 
    public override bool RequiresConstantRepaint() => Application.isPlaying;
 
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
 
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Testing", EditorStyles.boldLabel);
 
        StatusEffectRegistry registry = StatusEffectRegistry.Instance;
        if (registry == null)
        {
            EditorGUILayout.HelpBox("No StatusEffectRegistry found. It must be named \"StatusEffectRegistry\" " +
                                    "and sit inside a folder called \"Resources\".", MessageType.Error);
            return;
        }
        if (registry.Count == 0)
        {
            EditorGUILayout.HelpBox("The StatusEffectRegistry is empty. Select it and click \"Find All Effects\".",
                                    MessageType.Warning);
            return;
        }
 
        var receiver = (StatusEffectReceiver)target;
        bool live = Application.isPlaying && receiver.IsSpawned;
 
        string[] names = new string[registry.Count];
        for (int i = 0; i < names.Length; i++)
            names[i] = registry.Get(i) != null ? registry.Get(i).DisplayName : "(missing)";
 
        _selected = Mathf.Clamp(_selected, 0, names.Length - 1);
        _selected = EditorGUILayout.Popup("Effect", _selected, names);
 
        using (new EditorGUI.DisabledScope(!live))
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply")) receiver.RequestApply(registry.Get(_selected));
            if (GUILayout.Button("Clear All")) receiver.RequestClearAll();
            EditorGUILayout.EndHorizontal();
        }
 
        if (!live)
        {
            EditorGUILayout.HelpBox("The buttons work in Play mode on a spawned player or enemy.", MessageType.Info);
            return;
        }
 
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Active effects", EditorStyles.boldLabel);
        if (receiver.Count == 0) EditorGUILayout.LabelField("None");
 
        for (int i = 0; i < receiver.Count; i++)
        {
            StatusEntry entry = receiver.EntryAt(i);
            StatusEffect effect = registry.Get(entry.EffectId);
 
            string label = effect != null ? effect.DisplayName : $"#{entry.EffectId}";
            if (entry.Stacks > 1) label += $"  x{entry.Stacks}";
 
            float left = receiver.RemainingOf(entry);
            EditorGUILayout.LabelField(label, float.IsInfinity(left) ? "until removed" : $"{left:0.0} s left");
        }
    }
}
 
[CustomEditor(typeof(StatusEffectRegistry))]
public sealed class StatusEffectRegistryEditor : Editor
{
    public override void OnInspectorGUI()
    {
        if (GUILayout.Button("Find All Effects"))
        {
            ((StatusEffectRegistry)target).FindAll();
            serializedObject.Update();
        }
 
        EditorGUILayout.HelpBox("Click this again whenever you create a new effect asset.", MessageType.None);
        EditorGUILayout.Space();
        DrawDefaultInspector();
    }
}