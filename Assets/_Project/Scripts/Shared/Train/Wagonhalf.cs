using UnityEngine;

public sealed class Wagonhalf : MonoBehaviour
{
    [Tooltip("Additional explosive dmg this wagon half deals to the wagon when destroyed")]
    [SerializeField] private int _explosiveDamage = 0;

    [Tooltip("Objects on this half that swap to the borken material when a defect triggers. " + 
             "One per possible defect.")]
    [SerializeField] private GameObject[] _damagePoints;

    [Tooltip("Optional: where players spawn on this half. Leave empty and the builder " +
         "drops one in the middle of the wagon automatically.")]
    [SerializeField] private Transform[] _spawnAnchors;

    [Tooltip("Name shown in the train builder. Falls back to the prefab name.")]
    [SerializeField] private string _displayName;

    public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? name : _displayName;

    public Transform[] SpawnAnchors => _spawnAnchors;

    public int ExplosiveDamage => _explosiveDamage;
    public GameObject[] DamagePoints => _damagePoints;
}
