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

    [Header("Destruction")]
    [Tooltip("Wreck shown when this wagon is destroyed. Visual only: the half root's colliders stay, so players can still walk across. Inherits this half's rotation, so it flips with the back half.")]
    [SerializeField] private GameObject _destroyedPrefab;

    public bool IsDestroyed { get; private set; }

    public void SetDestroyed()
    {
        if (IsDestroyed) return;
        IsDestroyed = true;

        for (int i = 0; i < transform.childCount; i++)
            transform.GetChild(i).gameObject.SetActive(false);

        if (_destroyedPrefab != null)
        {
            GameObject wreck = Instantiate(_destroyedPrefab, transform);
            wreck.transform.localPosition = Vector3.zero;
            wreck.transform.localRotation = Quaternion.identity;
        }
    }
}
