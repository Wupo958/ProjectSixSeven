using UnityEngine;

namespace ProjectSixSeven.Shared.Track
{
    public abstract class TrackSegment
    {
        public float Length { get; protected set; }

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
