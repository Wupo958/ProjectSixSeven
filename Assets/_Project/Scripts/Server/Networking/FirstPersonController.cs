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

        controller.enabled = IsOwner;
 
        if (!IsOwner)
        {
            if (cameraTransform != null)
                cameraTransform.gameObject.SetActive(false);
            return;
        }
 
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

    }

    void Update()
    {

        if(!IsOwner)
        {
            return;
        }

        Look();
        Move();
    }

    void Look()
    {
        Vector2 look = lookAction.ReadValue<Vector2>() * lookSensitivity;

        transform.Rotate(Vector3.up * look.x);

        pitch = Mathf.Clamp(pitch - look.y, -maxPitch, maxPitch);
        cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void Move()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();
        Vector3 move = transform.right * input.x + transform.forward * input.y;

        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        if (jumpAction.WasPressedThisFrame() && controller.isGrounded)
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = move * moveSpeed + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
    }
}