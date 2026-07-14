using UnityEngine;

namespace ProjectSixSeven.Shared.Track
{
    /// Planar helpers. Track is solved in the XZ plane with headings measured as
    /// atan2(z, x), so a positive rotation turns left.
    public static class TrackMath
    {
        public static Vector3 Flatten(Vector3 v)
        {
            return new Vector3(v.x, 0f, v.z);
        }

        public static Vector3 FlattenDirection(Vector3 v)
        {
            Vector3 flat = Flatten(v);
            return flat.sqrMagnitude < 0.000001f ? Vector3.forward : flat.normalized;
        }

        public static Vector3 RotateXZ(Vector3 v, float angleRadians)
        {
            float cos = Mathf.Cos(angleRadians);
            float sin = Mathf.Sin(angleRadians);
            return new Vector3(v.x * cos - v.z * sin, 0f, v.x * sin + v.z * cos);
        }

        public static float Heading(Vector3 forward)
        {
            return Mathf.Atan2(forward.z, forward.x);
        }

        public static Vector3 Direction(float heading)
        {
            return new Vector3(Mathf.Cos(heading), 0f, Mathf.Sin(heading));
        }

        /// Signed angle from a to b in radians, positive when b is to the left of a.
        public static float SignedAngle(Vector3 from, Vector3 to)
        {
            float cross = from.x * to.z - from.z * to.x;
            float dot = from.x * to.x + from.z * to.z;
            return Mathf.Atan2(cross, dot);
        }
    }
}
