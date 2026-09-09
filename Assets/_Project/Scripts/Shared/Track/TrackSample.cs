using UnityEngine;

namespace ProjectSixSeven.Shared.Track
{
    public readonly struct TrackSample
    {
        public Vector3 Position { get; }
        public Vector3 Forward { get; }

        /// Signed curvature in 1/m. Positive turns left, negative right, zero on straights.
        public float Curvature { get; }

        public TrackSample(Vector3 position, Vector3 forward, float curvature)
        {
            Position = position;
            Forward = forward;
            Curvature = curvature;
        }

        public float Radius => Mathf.Approximately(Curvature, 0f)
            ? float.PositiveInfinity
            : 1f / Mathf.Abs(Curvature);
    }
}
