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

        if (!FindAnyObjectByType<GroundJeepEnemy>())
        {
            SpawnEnemy(jeepEnemy, -3);
        }

        if (!FindAnyObjectByType<AirPlaneEnemy>())
        {
            SpawnEnemy(planeEnemy, 25);
        }
    }

    private void SpawnEnemy(NetworkObject prefab, float height)
    {
        Vector3 trainPos = train.transform.position;
        Vector3 spawnPoint = new Vector3(trainPos.x + Random.Range(-100f, 100f), height, trainPos.z + Random.Range(-100f, -250f));
        NetworkObject enemy = Instantiate(prefab, spawnPoint, Quaternion.identity);
        enemy.Spawn();
    }
}
