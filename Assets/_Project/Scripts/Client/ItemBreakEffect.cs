using UnityEngine;

public sealed class ItemBreakEffect : MonoBehaviour
{
    private const int ShardCount = 8;
    private const float Lifetime = 0.7f;
    private const float ShardSize = 0.06f;
    private const float Gravity = 9.81f;
 
    private static Mesh s_cube;
 
    private Transform[] _shards;
    private Vector3[] _velocities;
    private Vector3[] _spins;
    private float[] _sizes;
    private float _age;
 
    public static void Play(ItemDefinition item, Transform car, Vector3 localPos, Renderer look)
    {
        Vector3 position = car != null ? car.TransformPoint(localPos) : localPos;
        Quaternion rotation = car != null ? car.rotation : Quaternion.identity;
 
        if (item != null && item.BreakEffect != null)
        {
            GameObject fx = Instantiate(item.BreakEffect, position, rotation, car);
            Destroy(fx, 5f); 
            return;
        }
 
        var root = new GameObject("ItemBreakEffect");
        root.transform.SetParent(car, false);
        root.transform.SetPositionAndRotation(position, rotation);
        root.AddComponent<ItemBreakEffect>().Burst(look != null ? look.sharedMaterial : null);
    }
 
    private void Burst(Material material)
    {
        if (s_cube == null)
        {
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            s_cube = temp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(temp);
        }
 
        _shards = new Transform[ShardCount];
        _velocities = new Vector3[ShardCount];
        _spins = new Vector3[ShardCount];
        _sizes = new float[ShardCount];
 
        for (int i = 0; i < ShardCount; i++)
        {
            var shard = new GameObject("Shard");
            shard.transform.SetParent(transform, false);
            _sizes[i] = ShardSize * Random.Range(0.6f, 1.4f);
            shard.transform.localScale = Vector3.one * _sizes[i];
            shard.AddComponent<MeshFilter>().sharedMesh = s_cube;
            MeshRenderer mr = shard.AddComponent<MeshRenderer>();
            if (material != null) mr.sharedMaterial = material;
 
            Vector3 dir = Random.onUnitSphere;
            dir.y = Mathf.Abs(dir.y) + 0.3f;
            _shards[i] = shard.transform;
            _velocities[i] = dir.normalized * Random.Range(1.5f, 3.5f);
            _spins[i] = Random.insideUnitSphere * 720f;
        }
    }
 
    private void Update()
    {
        float dt = Time.deltaTime;
        _age += dt;
 
        if (_age >= Lifetime)
        {
            Destroy(gameObject);
            return;
        }
 
        float shrink = 1f - _age / Lifetime;
        for (int i = 0; i < _shards.Length; i++)
        {
            _velocities[i].y -= Gravity * dt;
            _shards[i].localPosition += _velocities[i] * dt;
            _shards[i].localRotation *= Quaternion.Euler(_spins[i] * dt);
            _shards[i].localScale = Vector3.one * (_sizes[i] * shrink);
        }
    }
}