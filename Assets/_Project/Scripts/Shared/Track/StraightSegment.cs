using UnityEngine;

namespace ProjectSixSeven.Shared.Track
{
    public sealed class StraightSegment : TrackSegment
    {
        private readonly Vector3 _start;
        private readonly Vector3 _forward;

        public StraightSegment(Vector3 start, Vector3 forward, float length)
        {
            _start = TrackMath.Flatten(start);
            _forward = TrackMath.FlattenDirection(forward);
            Length = Mathf.Max(0f, length);
        }

        public override float MaxAbsCurvature => 0f;

        public override float CurvatureAt(float distance)
        {
            return 0f;
        }

        public override Vector3 SamplePosition(float distance)
        {
            return _start + _forward * Mathf.Clamp(distance, 0f, Length);
        }

        public override Vector3 SampleForward(float distance)
        {
            return _forward;
        }
    }
}
