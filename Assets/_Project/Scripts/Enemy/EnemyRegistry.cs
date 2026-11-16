using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "PCCMT/Enemy Registry", fileName = "EnemyRegistry")]
public sealed class EnemyRegistry : ScriptableObject
{
    [Tooltip("Every enemy type that can appear in a level's enemy pool. Order defines the network id, so do not reorder once builds are out.")]
    [SerializeField] private List<EnemyBase> _enemies = new List<EnemyBase>();

    public int Count => _enemies.Count;

    public EnemyBase Get(int id)
    {
        if (id < 0 || id >= _enemies.Count)
        {
            return null;
        }

        return _enemies[id];
    }
}
