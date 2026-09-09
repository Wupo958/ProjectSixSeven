using System.Collections;
using ProjectSixSeven.Shared;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public sealed class PlayerCarriageAttachment : NetworkBehaviour
{
    [Tooltip("How hard remote copies are pulled toward their networked pose. Higher is snappier.")]
    [SerializeField] private float remoteSmoothing = 15f;

    private readonly NetworkVariable<Vector3> _localPosition =
        new NetworkVariable<Vector3>(Vector3.zero,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private readonly NetworkVariable<float> _localYaw =
        new NetworkVariable<float>(0f,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private CharacterController _controller;
    private Transform _carriage;
    private Vector3 _lastCarriagePos;
    private Quaternion _lastCarriageRot;
    private bool _riding;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            StartCoroutine(BoardWhenReady());
        }
    }

    private IEnumerator BoardWhenReady()
    {
        while (Carriage.Main == null)
        {
            yield return null;
        }

        _carriage = Carriage.Main.transform;
        yield return null;

        Transform anchor = Carriage.Main.GetSpawnAnchor(OwnerClientId);

        bool wasEnabled = _controller.enabled;
        _controller.enabled = false;
        transform.SetPositionAndRotation(anchor.position, anchor.rotation);
        _controller.enabled = wasEnabled;

        _lastCarriagePos = _carriage.position;
        _lastCarriageRot = _carriage.rotation;
        _riding = true;
    }

    private void LateUpdate()
    {
        if (_carriage == null)
        {
            _carriage = Carriage.Main != null ? Carriage.Main.transform : null;
            if (_carriage == null)
            {
                return;
            }
        }

        if (IsOwner)
        {
            if (_riding)
            {
                CarryWithCarriage();
            }

            PublishLocalPose();
        }
        else
        {
            ApplyRemotePose();
        }
    }

    private void CarryWithCarriage()
    {
        Vector3 posNow = _carriage.position;
        Quaternion rotNow = _carriage.rotation;

        Quaternion rotDelta = rotNow * Quaternion.Inverse(_lastCarriageRot);
        Vector3 target = posNow + rotDelta * (transform.position - _lastCarriagePos);
        Vector3 carry = target - transform.position;

        if (_controller.enabled)
        {
            _controller.Move(carry);
        }
        else
        {
            transform.position = target;
        }

        float yawDelta = Vector3.SignedAngle(Flatten(_lastCarriageRot * Vector3.forward),
            Flatten(rotNow * Vector3.forward), Vector3.up);
        transform.Rotate(0f, yawDelta, 0f, Space.World);

        _lastCarriagePos = posNow;
        _lastCarriageRot = rotNow;
    }

    private void PublishLocalPose()
    {
        _localPosition.Value = _carriage.InverseTransformPoint(transform.position);
        _localYaw.Value = Vector3.SignedAngle(
            Flatten(_carriage.forward), Flatten(transform.forward), Vector3.up);
    }

    private void ApplyRemotePose()
    {
        Vector3 worldTarget = _carriage.TransformPoint(_localPosition.Value);
        Quaternion worldRot = Quaternion.LookRotation(Flatten(_carriage.forward), Vector3.up)
            * Quaternion.Euler(0f, _localYaw.Value, 0f);

        float t = 1f - Mathf.Exp(-remoteSmoothing * Time.deltaTime);
        transform.SetPositionAndRotation(
            Vector3.Lerp(transform.position, worldTarget, t),
            Quaternion.Slerp(transform.rotation, worldRot, t));
    }

    private static Vector3 Flatten(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude < 1e-6f ? Vector3.forward : v.normalized;
    }
}
