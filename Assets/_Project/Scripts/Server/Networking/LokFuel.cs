using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(TrainMotion))]
public sealed class LokFuel : NetworkBehaviour
{
    public static LokFuel Instance { get; private set; }
 
    [Tooltip("Tank size.")]
    [SerializeField, Min(1f)] private float _capacity = 100f;
 
    [Tooltip("Fuel in the tank when the level starts.")]
    [SerializeField, Min(0f)] private float _startFuel = 100f;
 
    [Tooltip("Fuel used per kilometre driven. At 30 m/s the train drives 1 km every 33 s, " +
             "so 10 per km empties a 100 tank in about 5.5 minutes.")]
    [SerializeField, Min(0f)] private float _usePerKm = 10f;
 
    [Tooltip("Fuel added by one Fuel item.")]
    [SerializeField, Min(0f)] private float _fuelPerItem = 25f;
 
    [Tooltip("Top speed (m/s) the train can still reach with an empty tank. 0 = it stops.")]
    [SerializeField, Min(0f)] private float _emptySpeedLimit = 0f;
 
    private struct Mark : INetworkSerializeByMemcpy
    {
        public float Fuel;       
        public float Distance;   
        public float UsePerMeter;
    }
 
    private readonly NetworkVariable<Mark> _mark = new NetworkVariable<Mark>();
 
    private TrainMotion _motion;
    private bool _empty;
 
    public float Capacity => _capacity;
    public float FuelPerItem => _fuelPerItem;
    public float Fraction => Fuel / _capacity;
    public bool IsEmpty => Fuel <= 0f;
    public bool IsFull => Fuel >= _capacity - 0.01f;
 
    public float Fuel
    {
        get
        {
            Mark mark = _mark.Value;
            float used = Mathf.Max(0f, _motion.Distance - mark.Distance) * mark.UsePerMeter;
            return Mathf.Clamp(mark.Fuel - used, 0f, _capacity);
        }
    }
 
    private void Awake()
    {
        Instance = this;
        _motion = GetComponent<TrainMotion>();
    }
 
    public override void OnDestroy()
    {
        if (Instance == this) Instance = null;
        base.OnDestroy();
    }
 
    public override void OnNetworkSpawn()
    {
        if (IsServer) SetFuel(_startFuel);
    }
 
    private void Update()
    {
        if (!IsServer || !IsSpawned) return;
 
        bool empty = IsEmpty;
        if (empty == _empty) return;
 
        _empty = empty;
        _motion.SetSpeedLimit(empty ? _emptySpeedLimit : float.PositiveInfinity);
        Debug.Log(empty ? "Lok out of fuel - the train slows down." : "Lok refuelled.");
    }
 
    // ----- server --------------------------------------------------------------
 
    public void AddFuel(float amount)
    {
        if (!IsServer || !IsSpawned) return;
        SetFuel(Fuel + amount);
    }
 
    public void SetFuel(float fuel)
    {
        if (!IsServer) return;
        _mark.Value = new Mark
        {
            Fuel = Mathf.Clamp(fuel, 0f, _capacity),
            Distance = _motion.Distance,
            UsePerMeter = _usePerKm / 1000f
        };
    }
 
    // ----- any machine -----------------------------------------------------------
 
    public void RequestRefuel()
    {
        if (IsServer) AddFuel(_fuelPerItem);
        else if (IsSpawned) RefuelServerRpc();
    }
 
    [ServerRpc(RequireOwnership = false)]
    private void RefuelServerRpc() => AddFuel(_fuelPerItem);
 
    public void RequestSetFuel(float fuel)
    {
        if (IsServer) SetFuel(fuel);
        else if (IsSpawned) SetFuelServerRpc(fuel);
    }
 
    [ServerRpc(RequireOwnership = false)]
    private void SetFuelServerRpc(float fuel) => SetFuel(fuel);
}