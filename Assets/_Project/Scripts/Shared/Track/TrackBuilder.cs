using System.Collections.Generic;
using UnityEngine;

namespace ProjectSixSeven.Shared.Track
{
    [ExecuteAlways]
    public sealed class TrackBuilder : MonoBehaviour
    {
        public enum TrackMode
        {
            Loop,

            PointToPoint
        }

        [Header("Nodes")]
        [SerializeField] private List<TrackNode> _nodes = new List<TrackNode>();
        [SerializeField] private TrackMode _mode = TrackMode.Loop;

        [Header("Railway limits")]
        [Tooltip("Tightest curve the track may ever take, in metres. Mainline rail sits around 300-800.")]
        [SerializeField] private float _minRadius = 300f;

        [Tooltip("Steepest slope allowed, as a ratio. 0.025 is 2.5%, a typical mainline ceiling.")]
        [SerializeField] private float _maxGrade = 0.025f;

        [Tooltip("Length of the clothoid easement used to ramp into a full-tightness curve, in metres. " +
                 "Longer means the train leans into curves more gently.")]
        [SerializeField] private float _transitionLength = 120f;

        [Header("Gizmos")]
        [SerializeField] private float _gizmoStep = 5f;
        [SerializeField] private bool _drawGizmos = true;

        private TrackPath _path;

        public TrackPath Path
        {
            get
            {
                if (_path == null)
                {
                    Rebuild();
                }

                return _path;
            }
        }

        public float MinRadius => _minRadius;
        public float MaxGrade => _maxGrade;
        public bool IsLoop => _mode == TrackMode.Loop;

        public float TransitionLength
        {
            get => _transitionLength;
            set => _transitionLength = Mathf.Max(0f, value);
        }

        private void Awake()
        {
            Rebuild();
        }

        private void OnValidate()
        {
            _minRadius = Mathf.Max(1f, _minRadius);
            _maxGrade = Mathf.Max(0.0001f, _maxGrade);
            _transitionLength = Mathf.Max(0f, _transitionLength);
            _gizmoStep = Mathf.Max(0.5f, _gizmoStep);
            _path = null;
        }

        public void Configure(List<TrackNode> nodes, TrackMode mode)
        {
            _nodes = nodes ?? new List<TrackNode>();
            _mode = mode;
            Rebuild();
        }

        public void Rebuild()
        {
            List<TrackSegment> segments = new List<TrackSegment>();

            int pairs = IsLoop ? _nodes.Count : _nodes.Count - 1;
            for (int i = 0; i < pairs; i++)
            {
                TrackNode from = _nodes[i];
                TrackNode to = _nodes[(i + 1) % _nodes.Count];

                if (from == null || to == null)
                {
                    continue;
                }

                List<TrackSegment> solved = RailwaySolver.Solve(
                    from.Position, from.Forward, to.Position, to.Forward, _minRadius, _transitionLength);

                if (solved == null)
                {
                    continue;
                }

                ApplyElevation(solved, from.Position.y, to.Position.y);
                segments.AddRange(solved);
            }

            _path = new TrackPath(segments);
        }

        private static void ApplyElevation(List<TrackSegment> segments, float startElevation, float endElevation)
        {
            float total = 0f;
            for (int i = 0; i < segments.Count; i++)
            {
                total += segments[i].Length;
            }

            if (total <= Mathf.Epsilon)
            {
                return;
            }

            float travelled = 0f;
            for (int i = 0; i < segments.Count; i++)
            {
                segments[i].StartElevation = Mathf.Lerp(startElevation, endElevation, travelled / total);
                travelled += segments[i].Length;
                segments[i].EndElevation = Mathf.Lerp(startElevation, endElevation, travelled / total);
            }
        }

        private void OnDrawGizmos()
        {
            if (!_drawGizmos)
            {
                return;
            }

            if (!Application.isPlaying)
            {
                Rebuild();
            }

            TrackPath path = _path;
            if (path == null || !path.IsValid)
            {
                return;
            }

            Vector3 previous = path.Sample(0f).Position;
            for (float d = _gizmoStep; d <= path.Length; d += _gizmoStep)
            {
                TrackSample sample = path.Sample(d);
                Gizmos.color = Mathf.Abs(SampleGrade(path, d)) > _maxGrade ? Color.red : Color.green;
                Gizmos.DrawLine(previous, sample.Position);
                previous = sample.Position;
            }
        }

        private static float SampleGrade(TrackPath path, float distance)
        {
            const float delta = 0.5f;
            float ahead = path.Sample(Mathf.Min(distance + delta, path.Length)).Position.y;
            float behind = path.Sample(Mathf.Max(distance - delta, 0f)).Position.y;
            return (ahead - behind) / (delta * 2f);
        }
    }
}
