using UnityEngine;

namespace ProjectSixSeven.Shared.Track
{
    /// Circular arc of fixed radius. A positive turn angle curves left, negative curves right.
    public sealed class ArcSegment : TrackSegment
    {
        private readonly Vector3 _start;
        private readonly Vector3 _forward;
        private readonly Vector3 _center;
        private readonly float _radius;
        private readonly float _sign;

        public ArcSegment(Vector3 start, Vector3 forward, float radius, float turnAngleRadians)
        {
            _start = TrackMath.Flatten(start);
            _forward = TrackMath.FlattenDirection(forward);
            _radius = Mathf.Max(0.001f, radius);
            _sign = Mathf.Sign(turnAngleRadians);

            Vector3 left = new Vector3(-_forward.z, 0f, _forward.x);
            _center = _start + left * (_sign * _radius);

            Length = Mathf.Abs(turnAngleRadians) * _radius;
        }

        public float Radius => _radius;

        public override float MaxAbsCurvature => 1f / _radius;

        public override float CurvatureAt(float distance)
        {
            return _sign / _radius;
        }

        public override Vector3 SamplePosition(float distance)
        {
            float delta = _sign * Mathf.Clamp(distance, 0f, Length) / _radius;
            return _center + TrackMath.RotateXZ(_start - _center, delta);
        }

        public override Vector3 SampleForward(float distance)
        {
            float delta = _sign * Mathf.Clamp(distance, 0f, Length) / _radius;
            return TrackMath.RotateXZ(_forward, delta).normalized;
        }
    }
}
