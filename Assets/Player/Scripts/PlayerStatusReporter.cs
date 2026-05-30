using UnityEngine;

[DisallowMultipleComponent]
public class PlayerStatusReporter : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerStatus playerStatus;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerAimSettings aimSettings;
    [SerializeField] private PlayerWeaponSlots playerWeaponSlots;
    [SerializeField] private Animator animator;

    [Header("Weapon Ammo Refs")]
    [SerializeField] private WeaponAmmoSettings rifleAmmoSettings;
    [SerializeField] private WeaponAmmoSettings shotgunAmmoSettings;
    [SerializeField] private WeaponAmmoSettings sniperAmmoSettings;

    [Header("Animator Params")]
    [SerializeField] private string isShootingBoolName = "IsShooting";
    [SerializeField] private string keepShootingBoolName = "KeepShooting";

    private bool hasIsShootingParam;
    private bool hasKeepShootingParam;

    private int isShootingHash;
    private int keepShootingHash;

    private void Reset()
    {
        AutoCacheRefs();
    }

    private void Awake()
    {
        AutoCacheRefs();
        CacheAnimatorParams();
    }

    private void OnEnable()
    {
        UploadAllStatuses();
    }

    private void OnDisable()
    {
        if (playerStatus == null)
            return;

        playerStatus.SetAiming(false);
        playerStatus.SetReloading(false);
        playerStatus.SetMovementStatus(false, false, false, false, false);
        playerStatus.ClearShooting();
    }

    private void LateUpdate()
    {
        UploadAllStatuses();
    }

    private void AutoCacheRefs()
    {
        if (playerStatus == null)
            playerStatus = GetComponent<PlayerStatus>();

        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();

        if (aimSettings == null)
            aimSettings = GetComponent<PlayerAimSettings>();

        if (playerWeaponSlots == null)
            playerWeaponSlots = GetComponent<PlayerWeaponSlots>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        WeaponAmmoSettings[] allAmmoSettings = GetComponentsInChildren<WeaponAmmoSettings>(true);

        for (int i = 0; i < allAmmoSettings.Length; i++)
        {
            WeaponAmmoSettings ammo = allAmmoSettings[i];

            if (ammo == null)
                continue;

            string ammoTypeName = ammo.AmmoType.ToString().ToLowerInvariant();

            if (rifleAmmoSettings == null &&
                (ammoTypeName.Contains("rifle") ||
                 ammoTypeName.Contains("ar") ||
                 ammoTypeName.Contains("assault")))
            {
                rifleAmmoSettings = ammo;
            }

            if (shotgunAmmoSettings == null &&
                (ammoTypeName.Contains("shotgun") ||
                 ammoTypeName.Contains("sg")))
            {
                shotgunAmmoSettings = ammo;
            }

            if (sniperAmmoSettings == null &&
                (ammoTypeName.Contains("sniper") ||
                 ammoTypeName.Contains("sr")))
            {
                sniperAmmoSettings = ammo;
            }
        }
    }

    private void CacheAnimatorParams()
    {
        isShootingHash = Animator.StringToHash(isShootingBoolName);
        keepShootingHash = Animator.StringToHash(keepShootingBoolName);

        hasIsShootingParam = HasAnimatorBool(isShootingBoolName);
        hasKeepShootingParam = HasAnimatorBool(keepShootingBoolName);
    }

    private bool HasAnimatorBool(string paramName)
    {
        if (animator == null)
            return false;

        if (string.IsNullOrWhiteSpace(paramName))
            return false;

        AnimatorControllerParameter[] parameters = animator.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter p = parameters[i];

            if (p.name == paramName && p.type == AnimatorControllerParameterType.Bool)
                return true;
        }

        return false;
    }

    private void UploadAllStatuses()
    {
        if (playerStatus == null)
            return;

        UploadMovementStatus();
        UploadAimingStatus();
        UploadReloadingStatus();
        UploadShootingStatus();
    }

    private void UploadMovementStatus()
    {
        if (playerMovement == null)
        {
            playerStatus.SetMovementStatus(false, false, false, false, false);
            return;
        }

        playerStatus.SetMovementStatus(
            playerMovement.IsCrouchingNow,
            playerMovement.IsProneNow,
            playerMovement.IsSlidingNow,
            playerMovement.IsDivingNow,
            playerMovement.IsRunningNow
        );
    }

    private void UploadAimingStatus()
    {
        bool aiming =
            aimSettings != null &&
            aimSettings.IsRealAimHeld;

        playerStatus.SetAiming(aiming);
    }

    private void UploadReloadingStatus()
    {
        bool reloading = false;

        WeaponAmmoSettings currentAmmo = GetCurrentAmmoSettings();

        if (currentAmmo != null && currentAmmo.IsReloading)
            reloading = true;

        if (!reloading && rifleAmmoSettings != null && rifleAmmoSettings.IsReloading)
            reloading = true;

        if (!reloading && shotgunAmmoSettings != null && shotgunAmmoSettings.IsReloading)
            reloading = true;

        if (!reloading && sniperAmmoSettings != null && sniperAmmoSettings.IsReloading)
            reloading = true;

        playerStatus.SetReloading(reloading);
    }

    private void UploadShootingStatus()
    {
        playerStatus.ClearShooting();

        if (!IsAnimatorShooting())
            return;

        WeaponAmmoSettings currentAmmo = GetCurrentAmmoSettings();

        if (currentAmmo == null)
            return;

        if (currentAmmo == rifleAmmoSettings)
        {
            playerStatus.SetRifleShooting(true);
            return;
        }

        if (currentAmmo == shotgunAmmoSettings)
        {
            playerStatus.SetShotgunShooting(true);
            return;
        }

        if (currentAmmo == sniperAmmoSettings)
        {
            playerStatus.SetSniperShooting(true);
            return;
        }

        string ammoTypeName = currentAmmo.AmmoType.ToString().ToLowerInvariant();

        if (ammoTypeName.Contains("shotgun") || ammoTypeName.Contains("sg"))
        {
            playerStatus.SetShotgunShooting(true);
            return;
        }

        if (ammoTypeName.Contains("sniper") || ammoTypeName.Contains("sr"))
        {
            playerStatus.SetSniperShooting(true);
            return;
        }

        if (ammoTypeName.Contains("rifle") ||
            ammoTypeName.Contains("ar") ||
            ammoTypeName.Contains("assault"))
        {
            playerStatus.SetRifleShooting(true);
            return;
        }

        playerStatus.SetRifleShooting(true);
    }

    private bool IsAnimatorShooting()
    {
        if (animator == null)
            return false;

        bool isShooting = false;
        bool keepShooting = false;

        if (hasIsShootingParam)
            isShooting = animator.GetBool(isShootingHash);

        if (hasKeepShootingParam)
            keepShooting = animator.GetBool(keepShootingHash);

        return isShooting || keepShooting;
    }

    private WeaponAmmoSettings GetCurrentAmmoSettings()
    {
        if (playerWeaponSlots != null && playerWeaponSlots.CurrentAmmoSettings != null)
            return playerWeaponSlots.CurrentAmmoSettings;

        if (rifleAmmoSettings != null && rifleAmmoSettings.gameObject.activeInHierarchy)
            return rifleAmmoSettings;

        if (shotgunAmmoSettings != null && shotgunAmmoSettings.gameObject.activeInHierarchy)
            return shotgunAmmoSettings;

        if (sniperAmmoSettings != null && sniperAmmoSettings.gameObject.activeInHierarchy)
            return sniperAmmoSettings;

        return null;
    }
}