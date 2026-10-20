using System.Collections.Generic;
using UnityEngine;

public sealed class Wagon : MonoBehaviour
{
    [SerializeField] private List<GameObject> _damagePoints = new List<GameObject>();
    [SerializeField] private Wagonhalf _front;
    [SerializeField] private Wagonhalf _back;
    [SerializeField] private int _defects;
    [SerializeField] private int _maxHp;

    public Wagonhalf Front => _front;
    public Wagonhalf Back => _back;

    public int Defects => _defects; //X dmg in GDD
    public int MaxHP => _maxHp; //Z = X * Y
    public IReadOnlyList<GameObject> DamagePoints => _damagePoints;

    public void Setup(Wagonhalf front, Wagonhalf back, int hpPerDefect) {
        _front = front;
        _back = back;

        _damagePoints.Clear();
        if (front != null && front.DamagePoints != null) _damagePoints.AddRange(front.DamagePoints);
        if (back != null && back.DamagePoints != null) _damagePoints.AddRange(back.DamagePoints);

        _defects = _damagePoints.Count;
        _maxHp = Defects * hpPerDefect; // Y = hpPerDefect
    }
}
