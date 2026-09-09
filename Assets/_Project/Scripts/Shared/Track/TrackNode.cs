using UnityEngine;

namespace ProjectSixSeven.Shared.Track
{
    public sealed class TrackNode : MonoBehaviour
    {
        [SerializeField] private float _gizmoSize = 8f;

        public Vector3 Position => transform.position;

        public Vector3 Forward
        {
            get
            {
                Vector3 flat = new Vector3(transform.forward.x, 0f, transform.forward.z);
                return flat.sqrMagnitude < 0.0001f ? Vector3.forward : flat.normalized;
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.4f, 0.8f, 1f);
            Gizmos.DrawSphere(Position, _gizmoSize * 0.15f);

            Vector3 tip = Position + Forward * _gizmoSize;
            Gizmos.DrawLine(Position, tip);

            Vector3 left = new Vector3(-Forward.z, 0f, Forward.x);
            Gizmos.DrawLine(tip, tip - Forward * (_gizmoSize * 0.3f) + left * (_gizmoSize * 0.15f));
            Gizmos.DrawLine(tip, tip - Forward * (_gizmoSize * 0.3f) - left * (_gizmoSize * 0.15f));
        }
    }
}
