using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectSixSeven.Client
{
    /// Follows the train, either from behind or from inside it. Press C to swap.
    ///
    /// The chase view lags and eases deliberately, so curves read as the train swinging through
    /// them. The cab view is rigid to the carriage, which is what the real game looks like: the
    /// interior is still and the world swings past outside.
    public sealed class TrainCamera : MonoBehaviour
    {
        public enum ViewMode
        {
            Chase,
            Cab
        }

        [SerializeField] private Transform _target;
        [SerializeField] private ViewMode _mode = ViewMode.Chase;

        [Header("Chase")]
        [Tooltip("Offset from the train, in the train's own space: back, up and across.")]
        [SerializeField] private Vector3 _chaseOffset = new Vector3(0f, 14f, -45f);
        [SerializeField] private float _chaseAimHeight = 2f;
        [SerializeField] private float _positionSmoothing = 0.35f;
        [SerializeField] private float _rotationSmoothing = 4f;

        [Header("Cab")]
        [Tooltip("Offset inside the carriage, in the train's own space.")]
        [SerializeField] private Vector3 _cabOffset = new Vector3(0f, 1.2f, 10f);

        private Vector3 _velocity;

        private void OnEnable()
        {
            if (_target != null)
            {
                SnapToTarget();
            }
        }

        private void LateUpdate()
        {
            if (_target == null)
            {
                return;
            }

            if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
            {
                _mode = _mode == ViewMode.Chase ? ViewMode.Cab : ViewMode.Chase;
                SnapToTarget();
                return;
            }

            if (_mode == ViewMode.Cab)
            {
                transform.SetPositionAndRotation(CabPosition(), _target.rotation);
                return;
            }

            transform.position = Vector3.SmoothDamp(
                transform.position, ChasePosition(), ref _velocity, _positionSmoothing);

            Quaternion wanted = Quaternion.LookRotation(AimPoint() - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, wanted, 1f - Mathf.Exp(-_rotationSmoothing * Time.deltaTime));
        }

        private void SnapToTarget()
        {
            _velocity = Vector3.zero;

            if (_mode == ViewMode.Cab)
            {
                transform.SetPositionAndRotation(CabPosition(), _target.rotation);
                return;
            }

            Vector3 position = ChasePosition();
            transform.SetPositionAndRotation(
                position, Quaternion.LookRotation(AimPoint() - position, Vector3.up));
        }

        private Vector3 ChasePosition()
        {
            return _target.position + _target.TransformDirection(_chaseOffset);
        }

        private Vector3 CabPosition()
        {
            return _target.position + _target.TransformDirection(_cabOffset);
        }

        private Vector3 AimPoint()
        {
            return _target.position + Vector3.up * _chaseAimHeight;
        }
    }
}
