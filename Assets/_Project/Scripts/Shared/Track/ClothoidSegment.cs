using UnityEngine;

namespace ProjectSixSeven.Shared.Track
{
    public sealed class ClothoidSegment : TrackSegment
    {
        private const float TargetSampleSpacing = 1f;

        private readonly Vector3[] _positions;
        private readonly float _startHeading;
        private readonly float _startCurvature;
        private readonly float _endCurvature;
        private readonly float _step;

        public ClothoidSegment(
            Vector3 start,
            Vector3 forward,
            float startCurvature,
            float endCurvature,
            float length)
        {
            Length = Mathf.Max(0.001f, length);
            _startCurvature = startCurvature;
            _endCurvature = endCurvature;
            _startHeading = TrackMath.Heading(TrackMath.FlattenDirection(forward));

            int steps = Mathf.Max(8, Mathf.CeilToInt(Length / TargetSampleSpacing));
            _step = Length / steps;
            _positions = new Vector3[steps + 1];
            _positions[0] = TrackMath.Flatten(start);

            for (int i = 0; i < steps; i++)
            {
                float midpoint = (i + 0.5f) * _step;
                Vector3 direction = TrackMath.Direction(HeadingAt(midpoint));
                _positions[i + 1] = _positions[i] + direction * _step;
            }
        }

        public override float MaxAbsCurvature => Mathf.Max(Mathf.Abs(_startCurvature), Mathf.Abs(_endCurvature));

        public override float CurvatureAt(float distance)
        {
            float t = Mathf.Clamp01(distance / Length);
            return Mathf.Lerp(_startCurvature, _endCurvature, t);
        }

        public override Vector3 SamplePosition(float distance)
        {
            float clamped = Mathf.Clamp(distance, 0f, Length);
            float exact = clamped / _step;
            int index = Mathf.Min(Mathf.FloorToInt(exact), _positions.Length - 2);
            float fraction = exact - index;

            return Vector3.Lerp(_positions[index], _positions[index + 1], fraction);
        }

        public override Vector3 SampleForward(float distance)
        {
            return TrackMath.Direction(HeadingAt(Mathf.Clamp(distance, 0f, Length)));
        }

        private float HeadingAt(float distance)
        {
            float rate = (_endCurvature - _startCurvature) / Length;
            return _startHeading + _startCurvature * distance + rate * distance * distance * 0.5f;
        }
    }
}
