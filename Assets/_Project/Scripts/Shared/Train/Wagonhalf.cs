using UnityEngine;

public sealed class Wagonhalf : MonoBehaviour
{
    [Tooltip("Additional explosive dmg this wagon half deals to the wagon when destroyed")]
    [SerializeField] private int _explosiveDamage = 0;

    [Tooltip("Objects on this half that swap to the borken material when a defect triggers. " + 
             "One per possible defect.")]
    [SerializeField] private GameObject[] _damagePoints;

    public int ExplosiveDamage => _explosiveDamage;
    public GameObject[] DamagePoints => _damagePoints;
}
