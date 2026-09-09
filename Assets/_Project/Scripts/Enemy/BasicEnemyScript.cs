using System;
using Unity.Netcode;
using UnityEngine;


public class BasicEnemyScript : NetworkBehaviour
{
    [SerializeField] private GameObject _train;
    [SerializeField] private GameObject _projectilePrefab;
    [SerializeField] private Transform _muzzle;
    [SerializeField] private float _shootInterval = 1f;
    [SerializeField] private float _coneAngle = 30f;
    [SerializeField] private float _maxRange = 200f;

    private Vector3 _lastTrainPos;
    private Vector3 _trainVelocity;
    private bool _hasLastTrainPos;

    public override void OnNetworkSpawn()
    {
        // only the server shoots, the bullets it spawns show up on every client
        if (IsServer)
        {
            InvokeRepeating(nameof(ShootTrain), 1f, _shootInterval);
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (!IsServer || !FindTrain() || Time.deltaTime <= 0f)
        {
            return;
        }

        Vector3 pos = _train.transform.position;
        if (_hasLastTrainPos)
        {
            _trainVelocity = (pos - _lastTrainPos) / Time.deltaTime;
        }

        _lastTrainPos = pos;
        _hasLastTrainPos = true;
    }

    private void ShootTrain()
    {
        if (!FindTrain())
        {
            return;
        }

        Transform spawnPoint = _muzzle != null ? _muzzle : transform;

        if (Vector3.Distance(spawnPoint.position, _train.transform.position) > _maxRange)
        {
            return;
        }

        Vector3 direction = (PredictAimPoint(spawnPoint.position) - spawnPoint.position).normalized;
        direction = RandomizeInCone(direction);

        GameObject projectile = Instantiate(_projectilePrefab, spawnPoint.position, Quaternion.LookRotation(direction));
        projectile.GetComponent<NetworkObject>().Spawn();
        projectile.GetComponent<BasicProjectileScript>().Launch(direction, _maxRange);
    }

    private Vector3 PredictAimPoint(Vector3 from)
    {
        float projectileSpeed = _projectilePrefab.GetComponent<BasicProjectileScript>().Speed;
        Vector3 trainPos = _train.transform.position;
        Vector3 aimPoint = trainPos;

        // guess where the train will be once the bullet gets there, twice is close enough
        for (int i = 0; i < 2; i++)
        {
            float travelTime = Vector3.Distance(from, aimPoint) / projectileSpeed;
            aimPoint = trainPos + _trainVelocity * travelTime;
        }

        return aimPoint;
    }

    private Vector3 RandomizeInCone(Vector3 direction)
    {
        float half = _coneAngle / 2f;
        // random tilt up/down and left/right so not every shot hits
        Quaternion spread = Quaternion.Euler(UnityEngine.Random.Range(-half, half), UnityEngine.Random.Range(-half, half), 0f);
        return spread * direction;
    }

    private bool FindTrain()
    {
        if (_train == null)
        {
            _train = Carriage.Main != null ? Carriage.Main.gameObject : null;
        }

        return _train != null;
    }
}
