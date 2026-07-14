using UnityEngine;

namespace ProjectSixSeven.Shared.Track
{
    /// Base class for track pieces. Segments are solved purely in the horizontal XZ plane;
    /// elevation is applied afterwards by TrackPath via StartElevation/EndElevation.
    public abstract class TrackSegment
    {
        public float Length { get; protected set; }

        /// Signed curvature in 1/m at a distance along the segment. Positive turns left.
        /// Varies along the segment on a clothoid, which is the whole point of one.
        public abstract float CurvatureAt(float distance);

        public abstract float MaxAbsCurvature { get; }

        public float StartElevation { get; set; }
        public float EndElevation { get; set; }

        public float Grade => Length > Mathf.Epsilon ? (EndElevation - StartElevation) / Length : 0f;

        public abstract Vector3 SamplePosition(float distance);
        public abstract Vector3 SampleForward(float distance);

        public Vector3 EndPosition => SamplePosition(Length);
        public Vector3 EndForward => SampleForward(Length);
    }
}
