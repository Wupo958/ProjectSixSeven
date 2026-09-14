using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
 
public sealed class WeaponStation : Interactable
{
    [SerializeField] private WeaponBase _weapon;
 
    [Tooltip("Where the player is parked while seated")]
    [SerializeField] private Transform _seatAnchor;
 
    [SerializeField] private Key _leaveKey = Key.F;
 
    private PlayerInteractor _localRider;
    private FirstPersonController _controller;
    private PlayerCarriageAttachment _attachment;
 
    //camera restore data
    private Transform _camera;
    private Transform _cameraOldParent;
    private Vector3 _cameraOldLocalPos;
    private Quaternion _cameraOldLocalRot;
 
    public override bool CanInteract(PlayerInteractor player)
    {
        if (_weapon == null) return false;
 
        ItemDefinition held = player.Inventory != null ? player.Inventory.Held : null;
        if (_weapon.NeedsAmmo && _weapon.AcceptsAmmo(held)) return true;
 
        return !_weapon.IsOccupied || _localRider == player;
    }
 
    public override string GetPrompt(PlayerInteractor player)
    {
        if (_weapon == null) return string.Empty;
 
        if (_localRider == player) return $"[{_leaveKey}] Leave gun";
 
        ItemDefinition held = player.Inventory != null ? player.Inventory.Held : null;
        if (_weapon.NeedsAmmo && _weapon.AcceptsAmmo(held))
            return $"[E] Load {held.DisplayName}";
 
        if (_weapon.IsOccupied) return "Gun is manned";
 
        if (_weapon.Loaded <= 0) return "[E] Man the gun (empty)";
        return $"[E] Man the gun ({_weapon.Loaded}/{_weapon.Definition.MagazineCapacity})";
    }
 
    public override void Interact(PlayerInteractor player)
    {
        if (_weapon == null) return;
 
        //loading takes priority: it works whether or not someone is seated
        ItemDefinition held = player.Inventory != null ? player.Inventory.Held : null;
        if (_weapon.NeedsAmmo && _weapon.AcceptsAmmo(held))
        {
            _weapon.LoadAmmoServerRpc(held.AmmoDamageMultiplier);
            player.Inventory.ConsumeHeld();
            return;
        }
 
        if (!_weapon.IsOccupied) Mount(player);
    }
 
    private void Update()
    {
        if (_localRider == null) return;
 
        if (Keyboard.current != null && Keyboard.current[_leaveKey].wasPressedThisFrame)
        {
            Dismount();
        }
    }
 
    private void Mount(PlayerInteractor player)
    {
        _localRider = player;
        _controller = player.GetComponentInParent<FirstPersonController>();
        _attachment = player.GetComponentInParent<PlayerCarriageAttachment>();
 
        //hand normal movement over to the weapon
        if (_controller != null) _controller.ControlSuspended = true;
        if (_attachment != null) _attachment.Seat = _seatAnchor;
 
        //move the players camera onto the gun so you sight along the barrel
        _camera = _controller != null ? _controller.cameraTransform : null;
        if (_camera != null && _weapon.CameraAnchor != null)
        {
            _cameraOldParent = _camera.parent;
            _cameraOldLocalPos = _camera.localPosition;
            _cameraOldLocalRot = _camera.localRotation;
 
            _camera.SetParent(_weapon.CameraAnchor, false);
            _camera.localPosition = Vector3.zero;
            _camera.localRotation = Quaternion.identity;
 
            Camera cam = _camera.GetComponent<Camera>();

            if (_weapon is CannonWeapon cannon) cannon.AttachCamera(cam);
            else if (_weapon is MachineGunWeapon mg) mg.AttachCamera(cam);
                
        }
 
        _weapon.BeginLocalControl(player);
        _weapon.RequestMountServerRpc(NetworkManager.Singleton.LocalClientId);
    }
 
    private void Dismount()
    {
        if (_weapon is CannonWeapon cannon) cannon.DetachCamera();
        else if (_weapon is MachineGunWeapon m) m.DetachCamera();
 
        if (_camera != null)
        {
            _camera.SetParent(_cameraOldParent, false);
            _camera.localPosition = _cameraOldLocalPos;
            _camera.localRotation = _cameraOldLocalRot;
            _camera = null;
        }
 
        if (_controller != null) _controller.ControlSuspended = false;
        if (_attachment != null) _attachment.Seat = null;
 
        _weapon.EndLocalControl();
        _weapon.RequestDismountServerRpc(NetworkManager.Singleton.LocalClientId);
 
        _localRider = null;
        _controller = null;
        _attachment = null;
    }
}
