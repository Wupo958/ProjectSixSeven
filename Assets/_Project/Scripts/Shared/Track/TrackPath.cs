using System.Collections.Generic;
using UnityEngine;

namespace ProjectSixSeven.Shared.Track
{
    public sealed class TrackPath
    {
        private readonly List<TrackSegment> _segments;
        private readonly float[] _startDistances;

        public TrackPath(List<TrackSegment> segments)
        {
            _segments = segments ?? new List<TrackSegment>();
            _startDistances = new float[_segments.Count];

            float total = 0f;
            for (int i = 0; i < _segments.Count; i++)
            {
                _startDistances[i] = total;
                total += _segments[i].Length;
            }

            Length = total;
        }

        public float Length { get; }
        public IReadOnlyList<TrackSegment> Segments => _segments;
        public bool IsValid => _segments.Count > 0 && Length > Mathf.Epsilon;

        public float SteepestGrade()
        {
            float steepest = 0f;
            for (int i = 0; i < _segments.Count; i++)
            {
                steepest = Mathf.Max(steepest, Mathf.Abs(_segments[i].Grade));
            }

            return steepest;
        }

        public float TightestRadius()
        {
            float highestCurvature = 0f;
            for (int i = 0; i < _segments.Count; i++)
            {
                highestCurvature = Mathf.Max(highestCurvature, _segments[i].MaxAbsCurvature);
            }

            return highestCurvature < 0.000001f ? float.PositiveInfinity : 1f / highestCurvature;
        }

        public TrackSample Sample(float distance)
        {
            if (!IsValid)
            {
                return new TrackSample(Vector3.zero, Vector3.forward, 0f);
            }

            distance = Mathf.Clamp(distance, 0f, Length);

            int index = FindSegment(distance);
            TrackSegment segment = _segments[index];
            float local = Mathf.Clamp(distance - _startDistances[index], 0f, segment.Length);

            Vector3 planar = segment.SamplePosition(local);
            Vector3 planarForward = segment.SampleForward(local);

            float t = segment.Length > Mathf.Epsilon ? local / segment.Length : 0f;
            float elevation = Mathf.Lerp(segment.StartElevation, segment.EndElevation, t);

            Vector3 forward = new Vector3(planarForward.x, segment.Grade, planarForward.z).normalized;
            Vector3 position = new Vector3(planar.x, elevation, planar.z);

            return new TrackSample(position, forward, segment.CurvatureAt(local));
        }

        private int FindSegment(float distance)
        {
            int low = 0;
            int high = _segments.Count - 1;

            while (low < high)
            {
                int mid = (low + high + 1) / 2;
                if (_startDistances[mid] <= distance)
                {
                    low = mid;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return low;
        }
    }
}
