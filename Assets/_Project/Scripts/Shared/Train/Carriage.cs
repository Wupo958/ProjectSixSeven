using Unity.Netcode;
using UnityEngine;

namespace ProjectSixSeven.Shared
{
    /// A single walkable train carriage. Players are parented to this object so they ride along in
    /// its local space: in the carriage's frame the floor is still, so walking is ordinary movement
    /// even while the train sweeps through curves at speed.
    ///
    /// The carriage carries a NetworkObject purely so it can be a parent for player NetworkObjects.
    /// It deliberately has no NetworkTransform: its pose is driven by TrainFollower from network time
    /// and is therefore already identical on every client, so there is nothing to replicate.
    [RequireComponent(typeof(NetworkObject))]
    public sealed class Carriage : MonoBehaviour
    {
        public static Carriage Main { get; private set; }

        [Tooltip("Direct children of the carriage where players are placed when they board. " +
                 "Cycled by client id so players don't stack on one spot.")]
        [SerializeField] private Transform[] _spawnAnchors;

        private NetworkObject _networkObject;

        public NetworkObject NetworkObject =>
            _networkObject != null ? _networkObject : _networkObject = GetComponent<NetworkObject>();

        private void OnEnable()
        {
            if (Main == null)
            {
                Main = this;
            }
        }

        private void OnDisable()
        {
            if (Main == this)
            {
                Main = null;
            }
        }

        /// Picks a boarding spot for a client. Deterministic in client id, so the owning client and
        /// the server agree on where a given player starts without an extra message.
        public Transform GetSpawnAnchor(ulong clientId)
        {
            if (_spawnAnchors == null || _spawnAnchors.Length == 0)
            {
                return transform;
            }

            int index = (int)(clientId % (ulong)_spawnAnchors.Length);
            return _spawnAnchors[index] != null ? _spawnAnchors[index] : transform;
        }
    }
}
