using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class SRShootSettings : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private MuzzlePointSettings MuzzlePointSettings;
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerCrossHairSettings PlayerCrossHairSettings;
    [SerializeField] private CameraNoiseByMovement cameraNoiseByMovement;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private WeaponAmmoSettings WeaponAmmoSettings;
    [SerializeField] private SR_DamageSetting damageSetting;

    [Header("Weapon SFX/VFX")]
    [SerializeField] private WeaponEffects weaponEffects;

    [Header("Aim Gate")]
    [SerializeField] private PlayerAimSettings aimSettings;
    [SerializeField] private bool allowQuickShot = false;

    [Header("Sniper Aim Move Lock")]
    [SerializeField] private bool lockMovementWhileAiming = true;
    [SerializeField] private bool alsoLockMovementDuringQuickShotAim = true;
    [SerializeField] private bool logAimMovementLock = false;

    [Header("Quick Shot Rotation")]
    [SerializeField] private bool useQuickShotRotationSpeedOverride = true;
    [Min(0f)] [SerializeField] private float quickShotRotationSpeed = 18f;

    [Header("Quick Shot Timing")]
    [SerializeField] private bool waitForQuickShotFacing = true;
    [Min(0f)] [SerializeField] private float quickShotMinWaitTime = 0.03f;
    [Min(0f)] [SerializeField] private float quickShotMaxWaitTime = 0.12f;
    [Min(0f)] [SerializeField] private float quickShotFireAngle = 6f;

    [Header("Projectile - Real Logic")]
    [SerializeField] private BulletProjectile bulletProjectilePrefab;
    [SerializeField] private Transform projectileSpawnOverride;
    [Min(0.01f)] [SerializeField] private float bulletSpeed = 260f;
    [Min(0.01f)] [SerializeField] private float bulletMaxDistance = 180f;
    [Min(0.001f)] [SerializeField] private float bulletRadius = 0.015f;
    [SerializeField] private LayerMask bulletHitMask = ~0;
    [SerializeField] private QueryTriggerInteraction projectileTriggerInteraction = QueryTriggerInteraction.Collide;

    [Header("Sniper Bullet Impact")]
    [SerializeField] private bool spawnBulletImpact = true;
    [SerializeField] private BulletImpact bulletImpactPrefab;
    [SerializeField] private Transform bulletImpactSpawnOverride;
    [Min(0.01f)] [SerializeField] private float bulletImpactSpeed = 120f;
    [Min(0.01f)] [SerializeField] private float bulletImpactMaxDistance = 180f;
    [SerializeField] private LayerMask bulletImpactHitMask = ~0;

    [Header("Projectile - Visual Only")]
    [SerializeField] private BulletProjectileVisual visualBulletProjectilePrefab;
    [SerializeField] private bool spawnVisualProjectile = true;
    [SerializeField] private Transform visualProjectileSpawnOverride;
    [Min(0.01f)] [SerializeField] private float visualBulletSpeed = 260f;

    [Header("Projectile - Visual Penetration")]
    [SerializeField] private bool visualPenetratesWhenWeaponPenetrates = true;
    [SerializeField] private bool forceVisualPenetration = false;
    [SerializeField] private LayerMask visualPenetrationStopMask = 0;
    [SerializeField] private QueryTriggerInteraction visualPenetrationTriggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("Fire Rate")]
    [Min(0f)] [SerializeField] private float shootCooldown = 1.25f;

    [Header("Spread")]
    [SerializeField] private bool useSpread = true;
    [SerializeField] private List<float> aimSpreadAngleList = new List<float> { 0.05f };
    [SerializeField] private List<float> quickShotSpreadAngleList = new List<float> { 8.0f };

    [Header("Recoil")]
    [SerializeField] private bool useRecoil = true;

    [Header("Animator")]
    [SerializeField] private string shootTriggerName = "Shoot";
    [SerializeField] private string quickShotBoolName = "QuickShot";
    [SerializeField] private string isShootingBoolName = "IsShooting";

    [Tooltip("Optional. For sniper, this will only be set briefly for one shot.")]
    [SerializeField] private string keepShootingBoolName = "KeepShooting";

    [SerializeField] private bool resetTriggerBeforeSet = true;
    [SerializeField] private float isShootingHoldTime = 0.16f;

    [Header("Input")]
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private string shootActionName = "Shoot";

    [Header("External Locks")]
    public bool externalShootLock = false;

    private InputAction shootAction;
    private float nextShootTime;

    private int shootTriggerHash;
    private int quickShotBoolHash;
    private int isShootingBoolHash;
    private int keepShootingBoolHash;

    private Coroutine shootingBoolRoutine;

    private bool quickShotSessionActive;
    private bool quickShotRequiresRelease;
    private bool quickShotPendingFire;
    private float quickShotPendingStartTime;

    private bool appliedAimMovementLock;

    private void Reset()
    {
        Transform root = transform.root;

        if (MuzzlePointSettings == null)
            MuzzlePointSettings = GetComponent<MuzzlePointSettings>();

        if (PlayerCrossHairSettings == null)
            PlayerCrossHairSettings = GetComponent<PlayerCrossHairSettings>();

        if (WeaponAmmoSettings == null)
            WeaponAmmoSettings = GetComponent<WeaponAmmoSettings>();

        if (damageSetting == null)
            damageSetting = GetComponent<SR_DamageSetting>();

        if (weaponEffects == null)
            weaponEffects = GetComponentInChildren<WeaponEffects>();

        if (aimSettings == null)
            aimSettings = root.GetComponent<PlayerAimSettings>();

        if (playerMovement == null)
            playerMovement = root.GetComponent<PlayerMovement>();

        if (playerInput == null)
            playerInput = root.GetComponent<PlayerInput>();

        if (animator == null)
            animator = root.GetComponentInChildren<Animator>();
    }

    private void Awake()
    {
        Transform root = transform.root;

        if (MuzzlePointSettings == null)
            MuzzlePointSettings = GetComponent<MuzzlePointSettings>();

        if (PlayerCrossHairSettings == null)
            PlayerCrossHairSettings = GetComponent<PlayerCrossHairSettings>();

        if (WeaponAmmoSettings == null)
            WeaponAmmoSettings = GetComponent<WeaponAmmoSettings>();

        if (damageSetting == null)
            damageSetting = GetComponent<SR_DamageSetting>();

        if (weaponEffects == null)
            weaponEffects = GetComponentInChildren<WeaponEffects>();

        if (aimSettings == null)
            aimSettings = root.GetComponent<PlayerAimSettings>();

        if (playerMovement == null)
            playerMovement = root.GetComponent<PlayerMovement>();

        if (playerInput == null)
            playerInput = root.GetComponent<PlayerInput>();

        if (animator == null)
            animator = root.GetComponentInChildren<Animator>();

        if (playerInput != null && playerInput.actions != null)
            shootAction = playerInput.actions[shootActionName];

        shootTriggerHash = Animator.StringToHash(shootTriggerName);
        quickShotBoolHash = Animator.StringToHash(quickShotBoolName);
        isShootingBoolHash = Animator.StringToHash(isShootingBoolName);
        keepShootingBoolHash = Animator.StringToHash(keepShootingBoolName);

        nextShootTime = 0f;
    }

    private void OnEnable()
    {
        ForceClearRuntimeState();
    }

    private void OnDisable()
    {
        if (shootingBoolRoutine != null)
        {
            StopCoroutine(shootingBoolRoutine);
            shootingBoolRoutine = null;
        }

        ForceClearRuntimeState();
        SetAimMovementLock(false);
    }

    private void Update()
    {
        bool realAimHeldNow = IsSniperAimHeldNow();
        UpdateAimMovementLock(realAimHeldNow);

        if (shootAction == null)
            return;

        bool shootPressedThisFrame = shootAction.WasPressedThisFrame();
        bool shootReleasedThisFrame = shootAction.WasReleasedThisFrame();

        if (shootReleasedThisFrame)
        {
            if (!quickShotPendingFire)
                ClearQuickShotState();

            SetAnimatorKeepShootingBool(false);
        }

        if (quickShotPendingFire)
            ProcessPendingQuickShotFire();

        if (!shootPressedThisFrame)
            return;

        if (realAimHeldNow)
        {
            ClearQuickShotState();
            Shoot();
            return;
        }

        if (!allowQuickShot || quickShotRequiresRelease)
            return;

        if (IsInShootCooldown())
            return;

        quickShotSessionActive = true;
        quickShotRequiresRelease = false;

        StartQuickShotPendingFire();
        ApplyQuickShotAimOverride();

        if (animator != null)
            animator.SetBool(quickShotBoolHash, true);

        ProcessPendingQuickShotFire();
    }

    private void LateUpdate()
    {
        UpdateAimMovementLock(IsSniperAimHeldNow());
    }

    private bool IsSniperAimHeldNow()
    {
        if (aimSettings == null)
            return false;

        return aimSettings.IsAimInputHeld ||
               aimSettings.IsAiming ||
               aimSettings.IsAimHeld;
    }

    private void ProcessPendingQuickShotFire()
    {
        if (!quickShotPendingFire)
            return;

        if (!quickShotSessionActive || quickShotRequiresRelease || !allowQuickShot)
        {
            quickShotPendingFire = false;
            return;
        }

        if (IsInShootCooldown())
        {
            ClearQuickShotState();
            return;
        }

        if (!IsQuickShotReadyToFire())
            return;

        quickShotPendingFire = false;

        Shoot();

        quickShotRequiresRelease = true;
        ClearQuickShotAimOverride();

        if (animator != null)
            animator.SetBool(quickShotBoolHash, false);
    }

    public void ForceClearRuntimeState()
    {
        quickShotSessionActive = false;
        quickShotRequiresRelease = false;
        quickShotPendingFire = false;
        externalShootLock = false;

        ClearQuickShotAimOverride();

        if (animator != null)
        {
            animator.SetBool(isShootingBoolHash, false);
            animator.SetBool(quickShotBoolHash, false);
        }

        SetAnimatorKeepShootingBool(false);
        SetAimMovementLock(false);
    }

    public void Shoot()
    {
        if (IsInShootCooldown())
            return;

        if (externalShootLock)
        {
            StopSingleShotState();
            return;
        }

        if (WeaponAmmoSettings != null)
        {
            if (WeaponAmmoSettings.IsReloading)
            {
                if (!WeaponAmmoSettings.TryCancelReloadForShoot())
                {
                    ClearShootStateWhenReloadBlocksShot();
                    return;
                }
            }
            else
            {
                if (!WeaponAmmoSettings.CanShoot())
                {
                    StopSingleShotState();
                    return;
                }
            }
        }

        bool realAimHeldNow = IsSniperAimHeldNow();
        bool isQuickShot = !realAimHeldNow && quickShotSessionActive && !quickShotRequiresRelease;
        bool isAimingShot = !isQuickShot;

        if (playerMovement != null)
        {
            if (isQuickShot)
            {
                if (!playerMovement.CanQuickShotNow)
                {
                    StopSingleShotState();
                    return;
                }
            }
            else
            {
                if (!playerMovement.CanAimShootNow)
                {
                    StopSingleShotState();
                    return;
                }
            }
        }

        if (isQuickShot && !allowQuickShot)
        {
            StopSingleShotState();
            return;
        }

        if (isQuickShot)
        {
            if (!quickShotSessionActive || quickShotRequiresRelease)
            {
                StopSingleShotState();
                return;
            }
        }

        if (MuzzlePointSettings == null || bulletProjectilePrefab == null)
        {
            StopSingleShotState();
            return;
        }

        if (WeaponAmmoSettings != null)
        {
            if (!WeaponAmmoSettings.TryConsumeOneRound())
            {
                StopSingleShotState();
                return;
            }
        }

        SetAnimatorKeepShootingBool(true);

        nextShootTime = Time.unscaledTime + Mathf.Max(0f, shootCooldown);

        if (PlayerCrossHairSettings != null && useRecoil)
        {
            if (isAimingShot)
                PlayerCrossHairSettings.AddAimShotRecoil();
            else if (isQuickShot)
                PlayerCrossHairSettings.AddQuickShotRecoil();
        }

        if (animator != null)
        {
            if (resetTriggerBeforeSet)
                animator.ResetTrigger(shootTriggerHash);

            animator.SetBool(quickShotBoolHash, isQuickShot);
            animator.SetTrigger(shootTriggerHash);
            animator.SetBool(isShootingBoolHash, true);

            if (shootingBoolRoutine != null)
                StopCoroutine(shootingBoolRoutine);

            shootingBoolRoutine = StartCoroutine(ResetIsShootingAfterDelay());
        }

        MuzzlePointSettings.RequestDebugDraw();

        Vector3 origin;
        Vector3 dir;

        if (TryGetViewShotRay(out Ray viewShotRay))
        {
            origin = viewShotRay.origin;
            dir = viewShotRay.direction;
        }
        else
        {
            origin = projectileSpawnOverride != null
                ? projectileSpawnOverride.position
                : MuzzlePointSettings.LastRay.origin;

            Vector3 targetPoint = ResolveShotTargetPoint(origin, isAimingShot);

            dir = targetPoint - origin;
            if (dir.sqrMagnitude <= 0.0001f)
                dir = MuzzlePointSettings.LastRay.direction;
        }

        dir.Normalize();

        float spreadAngle = GetRandomSpreadAngle(isAimingShot);
        dir = ApplySpread(dir, spreadAngle);

        Ray shotRay = new Ray(origin, dir);

        BulletProjectile bullet = Instantiate(bulletProjectilePrefab, origin, Quaternion.identity);
        bullet.Init(
            origin,
            dir,
            bulletSpeed,
            bulletMaxDistance,
            bulletRadius,
            bulletHitMask,
            projectileTriggerInteraction
        );

        if (damageSetting != null)
            damageSetting.ApplyToProjectile(bullet);

        SpawnVisualProjectile(shotRay);

        if (spawnBulletImpact)
            FireBulletImpact(origin, dir);

        if (weaponEffects != null)
            weaponEffects.PlayGunshot();
    }

    private void FireBulletImpact(Vector3 shotOrigin, Vector3 shotBaseDir)
    {
        if (bulletImpactPrefab == null)
            return;

        Vector3 spawnOrigin;

        if (bulletImpactSpawnOverride != null)
            spawnOrigin = bulletImpactSpawnOverride.position;
        else if (projectileSpawnOverride != null)
            spawnOrigin = projectileSpawnOverride.position;
        else if (MuzzlePointSettings != null)
            spawnOrigin = MuzzlePointSettings.LastRay.origin;
        else
            spawnOrigin = shotOrigin;

        Vector3 targetPoint = shotOrigin + shotBaseDir.normalized * Mathf.Max(0.01f, bulletImpactMaxDistance);
        Vector3 dir = targetPoint - spawnOrigin;

        if (dir.sqrMagnitude <= 0.0001f)
            dir = shotBaseDir;

        if (dir.sqrMagnitude <= 0.0001f)
            dir = transform.forward;

        dir.Normalize();

        BulletImpact impact = Instantiate(
            bulletImpactPrefab,
            spawnOrigin,
            Quaternion.LookRotation(dir, Vector3.up)
        );

        impact.Init(
            spawnOrigin,
            dir,
            bulletImpactSpeed,
            bulletImpactMaxDistance,
            bulletImpactHitMask,
            transform.root
        );
    }

    private void StartQuickShotPendingFire()
    {
        quickShotPendingFire = true;
        quickShotPendingStartTime = Time.unscaledTime;
    }

    private bool IsQuickShotReadyToFire()
    {
        if (!waitForQuickShotFacing)
            return true;

        float elapsed = Time.unscaledTime - quickShotPendingStartTime;

        if (elapsed < quickShotMinWaitTime)
            return false;

        if (elapsed >= quickShotMaxWaitTime)
            return true;

        if (playerMovement == null || playerMovement.ActiveView == null)
            return true;

        Vector3 aimDir = playerMovement.ActiveView.ViewAimWorldDir;
        aimDir.y = 0f;

        if (aimDir.sqrMagnitude <= 0.0001f)
            return false;

        aimDir.Normalize();

        Vector3 forward = playerMovement.transform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude <= 0.0001f)
            return true;

        forward.Normalize();

        float angle = Vector3.Angle(forward, aimDir);
        return angle <= quickShotFireAngle;
    }

    private bool IsInShootCooldown()
    {
        return shootCooldown > 0f && Time.unscaledTime < nextShootTime;
    }

    private void ApplyQuickShotAimOverride()
    {
        if (aimSettings != null)
        {
            if (useQuickShotRotationSpeedOverride)
                aimSettings.SetExternalAimOverride(true, quickShotRotationSpeed);
            else
                aimSettings.SetExternalAimOverride(true);
        }

        if (playerMovement != null && playerMovement.ActiveView != null)
        {
            if (useQuickShotRotationSpeedOverride)
                playerMovement.ActiveView.SetExternalAimOverride(true, quickShotRotationSpeed);
            else
                playerMovement.ActiveView.SetExternalAimOverride(true);
        }
    }

    private void ClearQuickShotAimOverride()
    {
        if (aimSettings != null)
            aimSettings.SetExternalAimOverride(false);

        if (playerMovement != null && playerMovement.ActiveView != null)
            playerMovement.ActiveView.SetExternalAimOverride(false);
    }

    private void ClearQuickShotState()
    {
        quickShotSessionActive = false;
        quickShotRequiresRelease = false;
        quickShotPendingFire = false;

        ClearQuickShotAimOverride();

        if (animator != null)
            animator.SetBool(quickShotBoolHash, false);
    }

    private void StopSingleShotState()
    {
        SetAnimatorKeepShootingBool(false);

        if (animator != null)
            animator.SetBool(isShootingBoolHash, false);
    }

    private void ClearShootStateWhenReloadBlocksShot()
    {
        quickShotSessionActive = false;
        quickShotRequiresRelease = true;
        quickShotPendingFire = false;

        ClearQuickShotAimOverride();

        if (animator != null)
        {
            animator.SetBool(quickShotBoolHash, false);
            animator.SetBool(isShootingBoolHash, false);
        }

        SetAnimatorKeepShootingBool(false);
    }

    private void SetAnimatorKeepShootingBool(bool value)
    {
        if (animator == null)
            return;

        if (string.IsNullOrWhiteSpace(keepShootingBoolName))
            return;

        animator.SetBool(keepShootingBoolHash, value);
    }

    private bool TryGetViewShotRay(out Ray shotRay)
    {
        shotRay = default;

        if (playerMovement == null)
            return false;

        if (playerMovement.ActiveView == null)
            return false;

        return playerMovement.ActiveView.TryGetViewShotRay(out shotRay);
    }

    private Vector3 ResolveShotTargetPoint(Vector3 origin, bool isAimingShot)
    {
        if (MuzzlePointSettings != null)
            return MuzzlePointSettings.AimPoint;

        if (TryGetQuickShotMousePoint(out Vector3 mousePoint))
            return mousePoint;

        return origin + transform.forward * bulletMaxDistance;
    }

    private bool TryGetQuickShotMousePoint(out Vector3 point)
    {
        point = Vector3.zero;

        Camera cam = null;

        if (aimSettings != null && aimSettings.aimCamera != null)
            cam = aimSettings.aimCamera;
        else
            cam = Camera.main;

        if (cam == null || Mouse.current == null)
            return false;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = cam.ScreenPointToRay(mousePos);

        if (PlayerCrossHairSettings != null)
        {
            if (PlayerCrossHairSettings.mouseAimLayers.value != 0 &&
                Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    PlayerCrossHairSettings.mouseRayMaxDistance,
                    PlayerCrossHairSettings.mouseAimLayers,
                    QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
                return true;
            }

            if (PlayerCrossHairSettings.mouseGroundLayers.value != 0 &&
                Physics.Raycast(
                    ray,
                    out RaycastHit groundHit,
                    PlayerCrossHairSettings.mouseRayMaxDistance,
                    PlayerCrossHairSettings.mouseGroundLayers,
                    QueryTriggerInteraction.Ignore))
            {
                point = groundHit.point;
                point.y += PlayerCrossHairSettings.mouseGroundHeightOffset;
                return true;
            }

            point = ray.origin + ray.direction * PlayerCrossHairSettings.mouseRayMaxDistance;
            return true;
        }

        if (aimSettings != null &&
            Physics.Raycast(
                ray,
                out RaycastHit aimHit,
                aimSettings.mouseAimDistance,
                aimSettings.mouseAimLayers,
                QueryTriggerInteraction.Ignore))
        {
            point = aimHit.point;
            return true;
        }

        point = ray.origin + ray.direction * bulletMaxDistance;
        return true;
    }

    private IEnumerator ResetIsShootingAfterDelay()
    {
        float maxDelay = shootCooldown > 0.02f
            ? shootCooldown - 0.01f
            : isShootingHoldTime;

        float delay = Mathf.Max(0.01f, Mathf.Min(isShootingHoldTime, maxDelay));

        yield return new WaitForSecondsRealtime(delay);

        if (animator != null)
        {
            animator.SetBool(isShootingBoolHash, false);
            animator.SetBool(keepShootingBoolHash, false);
        }

        shootingBoolRoutine = null;
    }

    private float GetRandomSpreadAngle(bool isAimingShot)
    {
        if (!useSpread)
            return 0f;

        List<float> list = isAimingShot ? aimSpreadAngleList : quickShotSpreadAngleList;

        if (list == null || list.Count == 0)
            return 0f;

        int index = Random.Range(0, list.Count);
        return Mathf.Max(0f, list[index]);
    }

    private Vector3 ApplySpread(Vector3 baseDir, float spreadAngleDeg)
    {
        baseDir = baseDir.sqrMagnitude > 0.0001f ? baseDir.normalized : Vector3.forward;

        if (!useSpread || spreadAngleDeg <= 0.001f)
            return baseDir;

        Quaternion basis = Quaternion.LookRotation(baseDir, Vector3.up);

        float tan = Mathf.Tan(spreadAngleDeg * Mathf.Deg2Rad);
        Vector2 offset = Random.insideUnitCircle * tan;

        Vector3 localDir = new Vector3(offset.x, offset.y, 1f).normalized;
        Vector3 spreadDir = basis * localDir;

        return spreadDir.normalized;
    }

    private void SpawnVisualProjectile(Ray shotRay)
    {
        if (!spawnVisualProjectile || visualBulletProjectilePrefab == null)
            return;

        Vector3 visualOrigin;

        if (visualProjectileSpawnOverride != null)
            visualOrigin = visualProjectileSpawnOverride.position;
        else if (projectileSpawnOverride != null)
            visualOrigin = projectileSpawnOverride.position;
        else
            visualOrigin = shotRay.origin;

        Vector3 targetPoint;

        bool visualShouldPenetrate = ShouldVisualPenetrate();

        LayerMask maskToUse = visualShouldPenetrate
            ? visualPenetrationStopMask
            : bulletHitMask;

        QueryTriggerInteraction triggerInteractionToUse = visualShouldPenetrate
            ? visualPenetrationTriggerInteraction
            : projectileTriggerInteraction;

        if (maskToUse.value != 0 &&
            Physics.Raycast(
                shotRay,
                out RaycastHit hit,
                bulletMaxDistance,
                maskToUse,
                triggerInteractionToUse))
        {
            targetPoint = hit.point;
        }
        else
        {
            targetPoint = shotRay.origin + shotRay.direction * bulletMaxDistance;
        }

        BulletProjectileVisual visualBullet =
            Instantiate(visualBulletProjectilePrefab, visualOrigin, Quaternion.identity);

        visualBullet.Init(visualOrigin, targetPoint, visualBulletSpeed);
    }

    private bool ShouldVisualPenetrate()
    {
        if (forceVisualPenetration)
            return true;

        if (!visualPenetratesWhenWeaponPenetrates)
            return false;

        return damageSetting != null && damageSetting.PenetrationCount > 0;
    }

    private void UpdateAimMovementLock(bool realAimHeldNow)
    {
        bool quickShotAimActive =
            alsoLockMovementDuringQuickShotAim &&
            quickShotSessionActive &&
            !quickShotRequiresRelease;

        bool shouldLock =
            lockMovementWhileAiming &&
            (realAimHeldNow || quickShotAimActive);

        SetAimMovementLock(shouldLock);
    }

    private void SetAimMovementLock(bool locked)
    {
        if (playerMovement == null)
        {
            if (logAimMovementLock)
                Debug.LogWarning("[SRShootSettings] Cannot set aim movement lock because playerMovement is null.", this);

            return;
        }

        if (appliedAimMovementLock == locked && playerMovement.externalMovementLock == locked)
            return;

        appliedAimMovementLock = locked;
        playerMovement.externalMovementLock = locked;

        if (logAimMovementLock)
        {
            Debug.Log(
                $"[SRShootSettings] Aim movement lock = {locked}, PlayerMovement = {playerMovement.name}",
                this
            );
        }
    }
}