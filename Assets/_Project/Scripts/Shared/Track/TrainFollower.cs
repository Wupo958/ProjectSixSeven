using UnityEngine;

namespace ProjectSixSeven.Shared.Track
{
    /// Drives a carriage along the track by arc length. The body is placed from two bogie samples
    /// rather than one point on the centreline, so it sits across a curve like a real rigid carriage
    /// instead of bending along it.
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
        [SerializeField] private bool _loopAtEnd = true;

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

            if (Application.isPlaying)
            {
                float elapsed = TimeSource != null ? TimeSource() : Time.timeSinceLevelLoad;
                _distance = _startDistance + _speed * elapsed;
            }
            else
            {
                _distance = _startDistance;
            }

            float length = _track.Path.Length;
            if (_distance > length)
            {
                _distance = _loopAtEnd ? _distance % length : length;
            }

            PlaceOnTrack();
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
