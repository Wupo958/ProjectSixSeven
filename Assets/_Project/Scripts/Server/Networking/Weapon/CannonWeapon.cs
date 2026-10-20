using UnityEngine;
using UnityEngine.InputSystem;
 
public sealed class CannonWeapon : WeaponBase
{
    [Header("Cannon input")]
    [SerializeField] private Key _pitchUpKey = Key.W;
    [SerializeField] private Key _pitchDownKey = Key.S;
    [SerializeField] private Key _yawLeftKey = Key.A;
    [SerializeField] private Key _yawRightKey = Key.D;
    [SerializeField] private Key _fireKey = Key.Space;
 
    [Tooltip("How fast the view zooms in and out.")]
    [SerializeField] private float _zoomLerp = 8f;
 
    private Camera _gunnerCamera;
    private float _baseFov;
    private float _zoomT;
 
    //weaponStation hands us the camera it moved onto the gun
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
        Keyboard kb = Keyboard.current;
        if (kb == null) return;
 
        //pitch: W raises the barrel, S lowers it
        float pitchInput = (kb[_pitchUpKey].isPressed ? 1f : 0f) - (kb[_pitchDownKey].isPressed ? 1f : 0f);
        Pitch = ClampPitch(Pitch + pitchInput * _definition.PitchSpeed * dt);
 
        //yaw: A/D swing the turret
        float yawInput = (kb[_yawRightKey].isPressed ? 1f : 0f) - (kb[_yawLeftKey].isPressed ? 1f : 0f);
        Yaw = ClampYaw(Yaw + yawInput * _definition.YawSpeed * dt);
 
        if (kb[_fireKey].wasPressedThisFrame) TryFire();
 
        UpdateZoom(dt);
    }
 
    private void UpdateZoom(float dt)
    {
        if (_gunnerCamera == null) return;
 
        bool zooming = Mouse.current != null && Mouse.current.rightButton.isPressed;
        _zoomT = Mathf.Lerp(_zoomT, zooming ? 1f : 0f, 1f - Mathf.Exp(-_zoomLerp * dt));
 
        //200% zoom means half the field of view
        float factor = Mathf.Max(1f, _definition.MaxZoomPercent / 100f);
        _gunnerCamera.fieldOfView = Mathf.Lerp(_baseFov, _baseFov / factor, _zoomT);
    }
}
