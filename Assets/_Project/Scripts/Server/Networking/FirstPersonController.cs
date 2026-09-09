using ProjectSixSeven.Shared;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : NetworkBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float gravity = -9.81f;
    public float jumpHeight = 1.2f;

    [Tooltip("Downward speed applied while grounded to keep the player pinned to the moving floor.")]
    public float groundStick = -4f;

    public float groundCheckDistance = 0.25f;

    [Header("Look")]
    public float lookSensitivity = 0.1f;
    public float maxPitch = 90f;

    [Tooltip("Drag the child Camera here.")]
    public Transform cameraTransform;

    CharacterController controller;
    InputAction moveAction;
    InputAction lookAction;
    InputAction jumpAction;

    float pitch;
    float verticalVelocity;
    bool controlEnabled;

    NetworkVariable<int> HEALTH= new NetworkVariable<int>(100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    void Awake()
    {
        controller = GetComponent<CharacterController>();

        moveAction = InputSystem.actions.FindAction("Move");
        lookAction = InputSystem.actions.FindAction("Look");
        jumpAction = InputSystem.actions.FindAction("Jump");

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
        
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        controller.enabled = false;

        if (cameraTransform != null)
            cameraTransform.gameObject.SetActive(false);

        if (IsOwner)
            TryEnableControl();
    }

    void TryEnableControl()
    {
        if (controlEnabled || !IsOwner || Carriage.Main == null)
            return;

        controlEnabled = true;
        controller.enabled = true;

        if (cameraTransform != null)
            cameraTransform.gameObject.SetActive(true);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (!IsOwner)
        {
            return;
        }

        if (!controlEnabled)
        {
            TryEnableControl();
            return;
        }

        Look();
        Move();
    }

    void Look()
    {
        if (lookAction == null || cameraTransform == null)
            return;

        Vector2 look = lookAction.ReadValue<Vector2>() * lookSensitivity;

        transform.Rotate(Vector3.up * look.x);

        pitch = Mathf.Clamp(pitch - look.y, -maxPitch, maxPitch);
        cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void Move()
    {
        Vector2 input = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        Vector3 move = transform.right * input.x + transform.forward * input.y;

        bool grounded = IsGrounded();

        if (grounded && verticalVelocity < 0f)
            verticalVelocity = groundStick;

        if (jumpAction != null && jumpAction.WasPressedThisFrame() && grounded)
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = move * moveSpeed + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
    }

    bool IsGrounded()
    {
        Vector3 origin = transform.position + controller.center;
        float distance = controller.height * 0.5f + groundCheckDistance;
        return Physics.Raycast(origin, Vector3.down, distance, ~0, QueryTriggerInteraction.Ignore);
    }
}