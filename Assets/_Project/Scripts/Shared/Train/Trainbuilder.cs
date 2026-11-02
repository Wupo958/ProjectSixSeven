using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectSixSeven.Shared.Track;

[ExecuteAlways]
public sealed class Trainbuilder : MonoBehaviour
{
    [Serializable]
    public sealed class WagonSlot {
        public Wagonhalf Front;
        public Wagonhalf Back;
    }

    [Header("Track")]
    [SerializeField] private TrackBuilder _track;

    [Header("Train pieces")]
    [Tooltip("Locomotive prefab")]
    [SerializeField] private GameObject _lokPrefab;
    
    [Tooltip("Editor-only preview layout. At runtime the layout comes from TrainConfig.")]
    [SerializeField] private List<WagonSlot> _wagons = new List<WagonSlot>();

    [Header("Metrics (metres)")]
    [SerializeField] private float _lokLength = 13f;
    [Tooltip("Local offset applied to each half. Leave at 0 if the half meshes are already " +
         "positioned correctly within their prefabs.")]
    [SerializeField] private float _halfOffset = 0f;
    [Tooltip("Distance from one wagon's centre to the next. Your full wagon length.")]
    [SerializeField] private float _wagonLength = 13f;

    [Tooltip("Fraction of a car's length between its two bogies")]
    [Range(0.3f, 0.95f)]
    [SerializeField] private float _bogieFraction = 0.7f;
    [Tooltip("Raises car bodies above the rail centreline")]
    [SerializeField] private float _rideHeight = 2f;
    [Tooltip("Rotate the back half 180 degrees so the two halves face each other.")]
    [SerializeField] private bool _flipBackHalf = true;


    [Header("Health")]
    [Tooltip("HP per defect (Y in GDD). Wagon HP = total defects * this")]
    [SerializeField] private int _hpPerDefect = 10;

    [Header("Spawning")]
    [Tooltip("Height above a wagon's origin for auto-generated spawn anchors, used when " +
             "a half prefab defines none of its own.")]
    [SerializeField] private float _spawnAnchorHeight = 1.1f;


    [Header("Movement")]
    [SerializeField] private float _speed = 30f;
    [SerializeField] private float _startDistance = 0f;
    [SerializeField] private float _startDelay = 5f;

    [Serializable]
    private struct PlacedCar {
        public Transform Car;
        public float CentreOffset; //distance from the trains nose to this cars centre in m
        public float Bogie; //current cars bogie spacing
    }

    [SerializeField, HideInInspector] private List<PlacedCar> _placed = new List<PlacedCar>();
    [SerializeField, HideInInspector] private float _trainLength;

    private const string ContainerName = "Generated";

    private float _noseDistance;
    public bool IsLoopTrack => _track != null && _track.IsLoop;
    public float TrackLength => _track != null && _track.Path != null ? _track.Path.Length : 0f;
    public float NoseDistance => _noseDistance;
    
    public float TrainLength => _trainLength;

    //ASSEMBLY

    [ContextMenu("Rebuild Train")]
    //EDITOR PREVIEW ONLY: Builds from inspector list
    public void Rebuild() {
        Build(_wagons.Count, i => _wagons[i] != null ? _wagons[i].Front : null, i => _wagons[i] != null ? _wagons[i].Back : null);
    }

    //RUNTIME: builds from the networked layout. Every client calls this with the same data, so every client ends up with an identical train.
    public void RebuildFrom(List<WagonSlotData> slots, WagonHalfRegistry registry) {
        if (slots == null || registry == null) return;
 
        Build(slots.Count, i => registry.Get(slots[i].Front), i => registry.Get(slots[i].Back));
    }

    private void Build(int wagonCount, Func<int, Wagonhalf> frontOf, Func<int, Wagonhalf> backOf) {
        Transform container = PrepareContainer();
        _placed = new List<PlacedCar>();
 
        var anchors = new List<Transform>();
        Transform lokTransform = null;
        Transform firstWagon = null;
 
        float nose = 0f; //offset grows backwards from the nose
 
        if (_lokPrefab != null) {
            GameObject lok = Instantiate(_lokPrefab, container);
            lok.name = "Lok";
            lokTransform = lok.transform;
            _placed.Add(new PlacedCar {
                Car = lok.transform,
                CentreOffset = nose + _lokLength * 0.5f,
                Bogie = _lokLength * _bogieFraction
            });
            nose += _lokLength;
        }
 
        for (int i = 0; i < wagonCount; i++) {
            var wagonGo = new GameObject($"Wagon {i + 1}");
            wagonGo.transform.SetParent(container, false);
            Wagon wagon = wagonGo.AddComponent<Wagon>();
 
            Wagonhalf front = InstantiateHalf(frontOf(i), wagonGo.transform, +_halfOffset, false);
            Wagonhalf back = InstantiateHalf(backOf(i), wagonGo.transform, -_halfOffset, _flipBackHalf);
            wagon.Setup(front, back, _hpPerDefect);
 
            if (firstWagon == null) firstWagon = wagonGo.transform;
 
            CollectSpawnAnchors(wagonGo.transform, front, back, anchors);
 
            _placed.Add(new PlacedCar {
                Car = wagonGo.transform,
                CentreOffset = nose + _wagonLength * 0.5f,
                Bogie = _wagonLength * _bogieFraction
            });
            nose += _wagonLength;
        }
<<<<<<< Updated upstream

=======
 
        //total length of the assembled train. The nose starts this far along the track so every car sits at a positive distance instead of clamping to 0
        _trainLength = nose;
 
>>>>>>> Stashed changes
        PlaceAll(EditDistance());
 
        //train tells Trainhealth what it is made of, instead of a designer re-dragging references after every rebuild.
        TrainHealth health = GetComponent<TrainHealth>();
        if (health != null) {
            health.CarryReference = lokTransform != null ? lokTransform : firstWagon != null ? firstWagon : transform;
            health.AimTarget = health.CarryReference;
            health.SetSpawnAnchors(anchors);
            health.Rebind();
        }

        TrainWeapons weapons = GetComponent<TrainWeapons>();
        if (weapons != null) weapons.Rebind();
    }

    private void CollectSpawnAnchors(Transform wagon, Wagonhalf front, Wagonhalf back, List<Transform> into) {
        int before = into.Count;
 
        AddHalfAnchors(front, into);
        AddHalfAnchors(back, into);
 
        if (into.Count == before) {
            var fallback = new GameObject("SpawnAnchor");
            fallback.transform.SetParent(wagon, false);
            fallback.transform.localPosition = new Vector3(0f, _spawnAnchorHeight, 0f);
            into.Add(fallback.transform);
        }
    }
 
    private static void AddHalfAnchors(Wagonhalf half, List<Transform> into) {
        if (half == null || half.SpawnAnchors == null) return;
        foreach (Transform t in half.SpawnAnchors)
            if (t != null) into.Add(t);
    }

    public void Awake() {
        PlaceAll(EditDistance());
    }

    private Wagonhalf InstantiateHalf(Wagonhalf prefab, Transform parent, float localZ, bool flip)
    {
        if (prefab == null) return null;
        Wagonhalf half = Instantiate(prefab, parent);
        half.transform.localPosition = new Vector3(0f, 0f, localZ);
        half.transform.localRotation = flip ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
        return half;
    }

    private Transform PrepareContainer() {
        Transform container = transform.Find(ContainerName);
        if (container == null) {
            container = new GameObject(ContainerName).transform;
            container.SetParent(transform, false);
        }

        for (int i = container.childCount - 1; i >= 0; i--) {
            GameObject child = container.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
        }
        return container;
    }

    //DRIVING

    private void Update() {
            
        if (_track == null || _track.Path == null || !_track.Path.IsValid || _placed == null) return;
 
        PlaceAll(Application.isPlaying ? PlayDistance() : EditDistance());
    }

    private float EditDistance() {
        float length = _track != null & _track.Path != null ? _track.Path.Length : 0f;
        return Mathf.Clamp(_startDistance + _trainLength, 0f, length);
    }

    private float PlayDistance() {
        float length = _track.Path.Length;
        float time = TrainFollower.TimeSource != null ? TrainFollower.TimeSource() : Time.timeSinceLevelLoad;
        float moving = Mathf.Max(0f, time - _startDelay);

        if (_track.IsLoop)
            return Mathf.Repeat(_startDistance + _speed * moving, length);
        
        if (length < 0.01f) return 0f;
        float omega = 2f * _speed / length;
        return 0.5f * length * (1f - Mathf.Cos(omega * moving));
    }

    private void PlaceAll(float nose) {
        if (_track == null || _track.Path == null || !_track.Path.IsValid) return;

        _noseDistance = nose;

        TrackPath path = _track.Path;

        for (int i = 0; i < _placed.Count; i++)
        {
            PlacedCar car = _placed[i];
            if (car.Car == null) continue;
 
            float centre = nose - car.CentreOffset;
            float half = car.Bogie * 0.5f;
 
            float fd = SampleDistance(centre + half, path.Length);
            float rd = SampleDistance(centre - half, path.Length);

            TrackSample f = path.Sample(fd);
            TrackSample r = path.Sample(rd);
 
            Vector3 axle = f.Position - r.Position;
            if (axle.sqrMagnitude < 0.0001f) continue;
 
            Quaternion rot = Quaternion.LookRotation(axle.normalized, Vector3.up);
            Vector3 seat = (f.Position + r.Position) * 0.5f;
            car.Car.SetPositionAndRotation(seat + rot * (Vector3.up * _rideHeight), rot);
        }
    }

    private float SampleDistance(float d, float length)
    {
        if (length < 0.01f) return 0f;
        return _track.IsLoop ? Mathf.Repeat(d, length) : Mathf.Clamp(d, 0f, length);
    }

}
