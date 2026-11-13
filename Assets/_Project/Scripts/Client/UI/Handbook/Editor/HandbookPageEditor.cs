using UnityEditor;
using UnityEngine;
 
[CustomEditor(typeof(HandbookPage))]
public sealed class HandbookPageEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var page = (HandbookPage)target;
        HandbookView view = Object.FindFirstObjectByType<HandbookView>(FindObjectsInactive.Include);
 
        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        bool changed = EditorGUI.EndChangeCheck();
 
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Write keys in brackets, e.g. [E], [Space], [LMB]: they're highlighted automatically. " +
            "<b>bold</b> and <i>italic</i> work too. Long pages shrink their text to fit.",
            MessageType.None);
 
        if (view == null)
        {
            EditorGUILayout.HelpBox("Open a scene with the Handbook UI (HandbookView) to preview this page.",
                                    MessageType.Info);
            return;
        }
 
        bool previewing = view.Shown == page;
 
        if (changed && previewing)
        {
            serializedObject.ApplyModifiedProperties();
            view.Show(page);
        }
 
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(previewing ? "Refresh Preview" : "Preview in Scene")) view.Show(page);
        using (new EditorGUI.DisabledScope(!previewing))
        {
            if (GUILayout.Button("Hide Preview")) view.Hide();
        }
        EditorGUILayout.EndHorizontal();
 
        if (!previewing) return;
 
        if (view.Overflowing)
        {
            EditorGUILayout.HelpBox(
                "This page doesn't fit even at the smallest text size. Shorten it, or move a section to the other side.",
                MessageType.Warning);
        }
        else if (view.LeftScale < 0.999f || view.RightScale < 0.999f)
        {
            EditorGUILayout.HelpBox(
                $"Text shrunk to fit: left {view.LeftScale:P0}, right {view.RightScale:P0}.",
                MessageType.Info);
        }
    }
}
