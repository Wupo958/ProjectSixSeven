using Unity.Netcode;
using UnityEngine;

public class EnemySpawner : NetworkBehaviour
{
    [SerializeField] private NetworkObject jeepEnemy;
    [SerializeField] private NetworkObject planeEnemy;
    [SerializeField] private GameObject train;

    // Update is called once per frame
    void Update()
    {
        if (!IsServer)
        {
            return;
        }

        int perType = EnemiesPerType();

        for (int i = EnemyBase.CountOf<GroundJeepEnemy>(); i < perType; i++)
        {
            SpawnEnemy(jeepEnemy, -3);
        }

        for (int i = EnemyBase.CountOf<AirPlaneEnemy>(); i < perType; i++)
        {
            SpawnEnemy(planeEnemy, 25);
        }
    }

    private int EnemiesPerType()
    {
        if (GameSession.Instance == null)
        {
            return 1;
        }

        return GameSession.Instance.LevelIndex + 1;
    }

    private void SpawnEnemy(NetworkObject prefab, float height)
    {
        Vector3 trainPos = train.transform.position;
        Vector3 spawnPoint = new Vector3(trainPos.x + Random.Range(-100f, 100f), height, trainPos.z + Random.Range(-100f, -250f));
        NetworkObject enemy = Instantiate(prefab, spawnPoint, Quaternion.identity);
        enemy.Spawn(true);
    }
}
