using UnityEngine;

public sealed class DripEffect : MonoBehaviour
{
    [Tooltip("The task whose strength sets the drip rate. Empty = steady Min Rate.")]
    [SerializeField] private MaintenanceTask _task;
 
    [Tooltip("Optional. Empty = plain black.")]
    [SerializeField] private Material _material;
 
    [Header("Drops")]
    [SerializeField, Min(0.01f)] private float _dropSize = 0.04f;
    [Tooltip("Drops per second when the task has just become due.")]
    [SerializeField, Min(0f)] private float _minRate = 1f;
    [Tooltip("Drops per second at full strength.")]
    [SerializeField, Min(0f)] private float _maxRate = 6f;
    [Tooltip("How far apart drops start, sideways (m).")]
    [SerializeField, Min(0f)] private float _spread = 0.08f;
    [Tooltip("Fall distance if no floor is found below (m).")]
    [SerializeField, Min(0.1f)] private float _defaultFall = 1.5f;
 
    private const int PoolSize = 16;
    private const float Gravity = 9.81f;
 
    private static Mesh s_sphere;
    private static Material s_black;
 
    private Transform[] _drops;
    private float[] _speeds;
    private float _fallDistance;
    private float _spawnTimer;
 
    private void Awake()
    {
        if (s_sphere == null)
        {
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            s_sphere = temp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(temp);
        }
 
        Material material = _material != null ? _material : BlackMaterial();
 
        _drops = new Transform[PoolSize];
        _speeds = new float[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            var drop = new GameObject("Drop");
            drop.transform.SetParent(transform, false);
            drop.transform.localScale = Vector3.one * _dropSize;
            drop.AddComponent<MeshFilter>().sharedMesh = s_sphere;
            drop.AddComponent<MeshRenderer>().sharedMaterial = material;
            drop.SetActive(false);
            _drops[i] = drop.transform;
        }
    }
 
    private void OnEnable()
    {
        _fallDistance = Physics.Raycast(transform.position, -transform.up, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore) ? hit.distance : _defaultFall;
 
        _spawnTimer = 0f;
        if (_drops == null) return;
        foreach (Transform drop in _drops) drop.gameObject.SetActive(false);
    }
 
    private void Update()
    {
        float dt = Time.deltaTime;
 
        float severity = _task != null ? _task.Severity : 0f;
        float rate = Mathf.Lerp(_minRate, _maxRate, severity);
        _spawnTimer += dt * rate;
        while (_spawnTimer >= 1f)
        {
            _spawnTimer -= 1f;
            Spawn();
        }
 
        for (int i = 0; i < _drops.Length; i++)
        {
            Transform drop = _drops[i];
            if (!drop.gameObject.activeSelf) continue;
 
            _speeds[i] += Gravity * dt;
            Vector3 p = drop.localPosition;
            p.y -= _speeds[i] * dt;
            drop.localPosition = p;
 
            if (-p.y >= _fallDistance) drop.gameObject.SetActive(false);
        }
    }
 
    private void Spawn()
    {
        for (int i = 0; i < _drops.Length; i++)
        {
            if (_drops[i].gameObject.activeSelf) continue;
 
            Vector2 offset = Random.insideUnitCircle * _spread;
            _drops[i].localPosition = new Vector3(offset.x, 0f, offset.y);
            _speeds[i] = 0f;
            _drops[i].gameObject.SetActive(true);
            return;
        }
    }
 
    private static Material BlackMaterial()
    {
        if (s_black != null) return s_black;
 
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
 
        s_black = new Material(shader);
        s_black.color = Color.black;
        if (s_black.HasProperty("_BaseColor")) s_black.SetColor("_BaseColor", Color.black);
        return s_black;
    }
}