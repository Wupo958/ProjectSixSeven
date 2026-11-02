using UnityEngine;
 
public enum FireMode { SingleShot = 0, Automatic = 1 }
 
//designer-authored weapon stats. Assets > Create > PCCMT > Weapon Definition.
//one asset per weapon type (Cannon, Light Gun, ...)
[CreateAssetMenu(menuName = "PCCMT/Weapon Definition", fileName = "Weapon_")]
public sealed class WeaponDefinition : ScriptableObject
{
    [Header("Identity")]
    public string DisplayName = "Cannon";
 
    [Tooltip("Ammo items whose Ammo Tag matches this can be loaded. e.g. \"HeavyAmmo\"")]
    public string AcceptedAmmoTag = "HeavyAmmo";
 
    [Header("Damage")]
    [Tooltip("Base damage per shot. Multiplied by the loaded ammo's multiplier")]
    public float Damage = 100f;
 
    [Header("Firing")]
    public FireMode FireMode = FireMode.SingleShot;
 
    [Tooltip("Only used when Fire Mode is Automatic")]
    public float RoundsPerMinute = 300f;
 
    [Tooltip("Shots held before a reload is needed")]
    public int MagazineCapacity = 1;
 
    [Tooltip("Cone half-angle of random spread, in degrees")]
    public float BulletSpread = 5f;
 
    [Tooltip("Upward kick applied to the barrel per shot, in degrees")]
    public float Recoil = 2f;

    [Tooltip("Rounds added per ammo item loaded. 1 for shell-fed guns, 100 for a belt/clip")]
    public int RoundsPerAmmoItem = 1;
 
    [Header("Projectile")]
    [Tooltip("Metres per second")]
    public float ProjectileSpeed = 100f;
 
    [Tooltip("Metres before the projectile despawns")]
    public float Range = 100f;
 
    [Header("Optics")]
    [Tooltip("200 means the view can zoom to 2x (half the field of view)")]
    public float MaxZoomPercent = 200f;
 
    [Header("Traverse - pitch (up/down)")]
    public float PitchSpeed = 15f;
    public float PitchMin = 0f;
    public float PitchMax = 60f;
 
    [Header("Traverse - yaw (left/right)")]
    public float YawSpeed = 15f;
 
    [Tooltip("Untick for a turret that can spin all the way round (no limits)")]
    public bool LimitYaw = false;
    public float YawMin = -180f;
    public float YawMax = 180f;
}
