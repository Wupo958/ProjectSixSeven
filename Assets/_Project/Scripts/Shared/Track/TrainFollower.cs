using UnityEngine;

namespace ProjectSixSeven.Shared.Track
{
    [ExecuteAlways]
    public sealed class TrainFollower : MonoBehaviour
    {
        public static System.Func<float> TimeSource;

        [SerializeField] private TrackBuilder _track;

        [Tooltip("Speed in metres per second. 30 is roughly 110 km/h.")]
        [SerializeField] private float _speed = 30f;

        [Tooltip("Distance between the front and rear bogie, in metres.")]
        [SerializeField] private float _bogieSpacing = 16f;

        [Tooltip("Raises the body above the rail surface, since the track centreline is the top of " +
                 "the rails. For the plain block this is just half its height.")]
        [SerializeField] private float _rideHeight = 2f;

        [SerializeField] private float _startDistance;

        [Tooltip("Seconds the train waits at the start before it begins moving, so players can spawn " +
                 "and settle onto the floor. Measured on the shared network clock, so every client " +
                 "starts moving together.")]
        [SerializeField] private float _startDelay = 5f;

        private float _distance;

        public float Distance => _distance;
        public float Speed => _speed;

        private void OnEnable()
        {
            _distance = _startDistance;
        }

        private void Update()
        {
            if (_track == null || _track.Path == null || !_track.Path.IsValid)
            {
                return;
            }

            float length = _track.Path.Length;

            if (Application.isPlaying)
            {
                float time = TimeSource != null ? TimeSource() : Time.timeSinceLevelLoad;
                float moving = Mathf.Max(0f, time - _startDelay);
                _distance = _track.IsLoop ? LoopDistance(moving, length) : ShuttleDistance(moving, length);
            }
            else
            {
                _distance = Mathf.Clamp(_startDistance, 0f, length);
            }

            PlaceOnTrack();
        }

        private float LoopDistance(float moving, float length)
        {
            return Mathf.Repeat(_startDistance + _speed * moving, length);
        }

        private float ShuttleDistance(float moving, float length)
        {
            if (length < 0.01f)
            {
                return 0f;
            }

            float omega = 2f * _speed / length;
            return 0.5f * length * (1f - Mathf.Cos(omega * moving));
        }

        private void PlaceOnTrack()
        {
            TrackPath path = _track.Path;
            float half = _bogieSpacing * 0.5f;

            TrackSample front = path.Sample(Mathf.Clamp(_distance + half, 0f, path.Length));
            TrackSample rear = path.Sample(Mathf.Clamp(_distance - half, 0f, path.Length));

            Vector3 axle = front.Position - rear.Position;
            if (axle.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Quaternion rotation = Quaternion.LookRotation(axle.normalized, Vector3.up);
            Vector3 seat = (front.Position + rear.Position) * 0.5f;

            transform.SetPositionAndRotation(seat + rotation * (Vector3.up * _rideHeight), rotation);
        }
    }
}
