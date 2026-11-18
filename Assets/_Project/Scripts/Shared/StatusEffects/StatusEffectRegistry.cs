using System.Collections.Generic;
using UnityEngine;
 
[CreateAssetMenu(menuName = "Status Effects/Registry", fileName = "StatusEffectRegistry")]
public sealed class StatusEffectRegistry : ScriptableObject
{
    private const string ResourcePath = "StatusEffectRegistry";
 
    [SerializeField] private List<StatusEffect> _effects = new List<StatusEffect>();
 
    private static StatusEffectRegistry s_instance;
 
    public static StatusEffectRegistry Instance
    {
        get
        {
            if (s_instance == null) s_instance = Resources.Load<StatusEffectRegistry>(ResourcePath);
            return s_instance;
        }
    }
 
    public int Count => _effects.Count;
    public int IdOf(StatusEffect effect) => effect != null ? _effects.IndexOf(effect) : -1;
    public StatusEffect Get(int id) => id >= 0 && id < _effects.Count ? _effects[id] : null;
 
#if UNITY_EDITOR
    public void FindAll()
    {
        _effects.Clear();
        foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:StatusEffect"))
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            StatusEffect effect = UnityEditor.AssetDatabase.LoadAssetAtPath<StatusEffect>(path);
            if (effect != null) _effects.Add(effect);
        }
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"StatusEffectRegistry: found {_effects.Count} effects.");
    }
#endif
}
 