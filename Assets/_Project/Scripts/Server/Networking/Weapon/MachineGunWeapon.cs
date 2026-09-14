using UnityEngine;
using UnityEngine.InputSystem;
 
public sealed class MachineGunWeapon : WeaponBase
{
    [Header("Mouse aim")]
    [Tooltip("Base mouse sensitivity, scaled by the definition's Pitch/Yaw Speed.")]
    [SerializeField] private float _mouseSensitivity = 0.05f;
 
    [SerializeField] private bool _invertPitch = false;
 
    [Header("Zoom")]
    [SerializeField] private float _zoomLerp = 8f;
 
    private Camera _gunnerCamera;
    private float _baseFov;
    private float _zoomT;
 
    public void AttachCamera(Camera cam)
    {
        _gunnerCamera = cam;
        if (cam != null) _baseFov = cam.fieldOfView;
        _zoomT = 0f;
    }
 
    public void DetachCamera()
    {
        if (_gunnerCamera != null) _gunnerCamera.fieldOfView = _baseFov;
        _gunnerCamera = null;
    }
 
    protected override void HandleGunnerInput(float dt)
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;
 
        Vector2 delta = mouse.delta.ReadValue() * _mouseSensitivity;
 
        //definition speeds scale the sensitivity so designers still tune per weapon
        float pitchDelta = delta.y * _definition.PitchSpeed;
        float yawDelta = delta.x * _definition.YawSpeed;
 
        Pitch = ClampPitch(Pitch + (_invertPitch ? -pitchDelta : pitchDelta));
        Yaw = ClampYaw(Yaw + yawDelta);
 
        //automatic: hold to keep firing. TryFire enforces the rate of fire
        if (mouse.leftButton.isPressed) TryFire();
 
        UpdateZoom(dt);
    }
 
    private void UpdateZoom(float dt)
    {
        if (_gunnerCamera == null) return;
 
        float factor = Mathf.Max(1f, _definition.MaxZoomPercent / 100f);
        if (Mathf.Approximately(factor, 1f))
        {
            _gunnerCamera.fieldOfView = _baseFov;   //100% means no magnification
            return;
        }
 
        bool zooming = Mouse.current != null && Mouse.current.rightButton.isPressed;
        _zoomT = Mathf.Lerp(_zoomT, zooming ? 1f : 0f, 1f - Mathf.Exp(-_zoomLerp * dt));
        _gunnerCamera.fieldOfView = Mathf.Lerp(_baseFov, _baseFov / factor, _zoomT);
    }
}
