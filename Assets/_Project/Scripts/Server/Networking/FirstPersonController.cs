using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : NetworkBehaviour
{
    [Header("Speeds (m/s)")]
    public float walkSpeed = 4f;
    public float sprintSpeed = 8f;
    public float crouchSpeed = 2f;

    [Header("Jumping")]
    [Tooltip("Seconds in the air per jump. Jump distance = speed x airtime (GDD: 1 s).")]
    public float jumpAirtime = 1f;
    public float gravity = -9.81f;

    [Tooltip("Downward speed applied while grounded to keep the player pinned to the moving floor.")]
    public float groundStick = -4f;
    public float groundCheckDistance = 0.25f;

    [Header("Crouching")]
    [Tooltip("Hitbox height while crouched, as a fraction of standing height (GDD: half).")]
    [Range(0.2f, 1f)]
    public float crouchHeightFactor = 0.5f;

    [Tooltip("Off: hold to crouch. On: press to toggle.")]
    public bool toggleCrouch = false;

    [Tooltip("How quickly hitbox and camera move between standing and crouching.")]
    public float crouchTransitionSpeed = 10f;

    [Tooltip("Optional: the visible model, as a CHILD of the player. It squashes when crouching, so other players can see it.")]
    public Transform crouchVisual;

    [Header("Look")]
    public float lookSensitivity = 0.1f;
    public float maxPitch = 90f;

    [Tooltip("Drag the child Camera here.")]
    public Transform cameraTransform;

    public float SpeedMultiplier { get; set; } = 1f;
    public bool MovementReversed { get; set; }

    public bool ControlSuspended { get; set; }
    public bool IsCrouched => _crouched.Value;
    public bool IsSprinting { get; private set; }

    static readonly Collider[] _overlap = new Collider[16];

    CharacterController controller;
    InputAction moveAction;
    InputAction lookAction;
    InputAction jumpAction;
    InputAction sprintAction;
    InputAction crouchAction;

    float pitch;
    float verticalVelocity;
    float airSpeed;
    bool controlEnabled;
    bool wantsCrouch;

    float standHeight;
    Vector3 standCenter;
    Vector3 standCameraPos;
    float crouchBlend;          //0 = standing, 1 = crouched (owner: hitbox + camera)
    float visualBlend;          //same, for the visible model on every client
    Vector3 visualBaseScale;
    Vector3 visualBasePos;

    NetworkVariable<int> HEALTH= new NetworkVariable<int>(100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    readonly NetworkVariable<bool> _crouched = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    void Awake()
    {
        controller = GetComponent<CharacterController>();
 
        moveAction = InputSystem.actions.FindAction("Move");
        lookAction = InputSystem.actions.FindAction("Look");
        jumpAction = InputSystem.actions.FindAction("Jump");
        sprintAction = InputSystem.actions.FindAction("Sprint");
        crouchAction = InputSystem.actions.FindAction("Crouch");
 
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
 
        standHeight = controller.height;
        standCenter = controller.center;
        if (cameraTransform != null) standCameraPos = cameraTransform.localPosition;
 
        if (crouchVisual != null)
        {
            visualBaseScale = crouchVisual.localScale;
            visualBasePos = crouchVisual.localPosition;
        }
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
        if (controlEnabled || !IsOwner || TrainHealth.Instance == null || !TrainHealth.Instance.IsBuilt)
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
        UpdateVisual(Time.deltaTime);
 
        if (!IsOwner)
            return;
 
        if (!controlEnabled)
        {
            TryEnableControl();
            return;
        }
 
        if (ControlSuspended)
            return;
 
        Look();
        UpdateCrouch(Time.deltaTime);
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

    // ----- crouching -------------------------------------------------------
 
    void UpdateCrouch(float dt)
    {
        if (crouchAction != null)
        {
            if (toggleCrouch)
            {
                if (crouchAction.WasPressedThisFrame()) wantsCrouch = !wantsCrouch;
            }
            else
            {
                wantsCrouch = crouchAction.IsPressed();
            }
        }
 
        //stay down while something is overhead (e.g. under a gun barrel).
        bool crouch = wantsCrouch || (_crouched.Value && !CanStandUp());
        if (crouch != _crouched.Value) _crouched.Value = crouch;
 
        crouchBlend = Mathf.MoveTowards(crouchBlend, crouch ? 1f : 0f, crouchTransitionSpeed * dt);
 
        float height = Mathf.Lerp(standHeight, standHeight * crouchHeightFactor, crouchBlend);
        float drop = standHeight - height;
 
        controller.height = height;
        controller.center = standCenter - new Vector3(0f, drop * 0.5f, 0f); //feet stay on the floor
 
        if (cameraTransform != null && cameraTransform.parent == transform)
            cameraTransform.localPosition = standCameraPos - new Vector3(0f, drop, 0f);
    }
 
    bool CanStandUp()
    {
        if (controller.height >= standHeight - 0.001f) return true;

        Vector3 s = transform.lossyScale;
        float scaleY = Mathf.Abs(s.y);
        float radius = controller.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z)) * 0.9f;

        Vector3 up = transform.up;
        Vector3 feet = transform.TransformPoint(controller.center) - up * (controller.height * scaleY * 0.5f);

        Vector3 bottom = feet + up * (radius + 0.05f);
        Vector3 top = feet + up * (standHeight * scaleY - radius);

        int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, _overlap, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            if (!_overlap[i].transform.IsChildOf(transform)) return false;
        }
        return true;
    }
 
    void UpdateVisual(float dt)
    {
        if (crouchVisual == null) return;
 
        visualBlend = Mathf.MoveTowards(visualBlend, _crouched.Value ? 1f : 0f, crouchTransitionSpeed * dt);
        float factor = Mathf.Lerp(1f, crouchHeightFactor, visualBlend);
        float drop = standHeight * (1f - factor);
 
        crouchVisual.localScale = new Vector3(visualBaseScale.x, visualBaseScale.y * factor, visualBaseScale.z);
        crouchVisual.localPosition = visualBasePos - new Vector3(0f, drop * 0.5f, 0f);
    }


    // ----- moving ----------------------------------------------------------
 
    float GroundSpeed()
    {
        if (_crouched.Value) return crouchSpeed;
        return IsSprinting ? sprintSpeed : walkSpeed;
    }
 
    void Move()
    {
        Vector2 input = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        if (MovementReversed) input = -input;
 
        bool grounded = IsGrounded();
 
        if (grounded)
        {
            IsSprinting = !_crouched.Value && sprintAction != null && sprintAction.IsPressed()
                          && input.sqrMagnitude > 0.01f;
            airSpeed = GroundSpeed();
 
            if (verticalVelocity < 0f)
                verticalVelocity = groundStick;
 
            if (jumpAction != null && jumpAction.WasPressedThisFrame())
                verticalVelocity = -gravity * jumpAirtime * 0.5f;
        }
 
        float speed = (grounded ? GroundSpeed() : airSpeed) * SpeedMultiplier;
 
        Vector3 move = transform.right * input.x + transform.forward * input.y;
        if (move.sqrMagnitude > 1f) move.Normalize();
 
        verticalVelocity += gravity * Time.deltaTime;
 
        CollisionFlags flags = controller.Move((move * speed + Vector3.up * verticalVelocity) * Time.deltaTime);
 
        if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
            verticalVelocity = 0f;
    }
 
    bool IsGrounded()
    {//
        Vector3 origin = transform.TransformPoint(controller.center);
        float distance = controller.height * Mathf.Abs(transform.lossyScale.y) * 0.5f + groundCheckDistance;
        return Physics.Raycast(origin, Vector3.down, distance, ~0, QueryTriggerInteraction.Ignore);
    }

}