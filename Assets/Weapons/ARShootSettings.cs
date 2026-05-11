using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class ARShootSettings : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private MuzzlePointSettings MuzzlePointSettings;
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerCrossHairSettings PlayerCrossHairSettings;
    [SerializeField] private CameraNoiseByMovement cameraNoiseByMovement;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private WeaponAmmoSettings WeaponAmmoSettings;
    [SerializeField] private AR_DamageSetting damageSetting;

    [Header("Weapon SFX/VFX")]
    [SerializeField] private WeaponEffects weaponEffects;

    [Header("Aim Gate")]
    [SerializeField] private PlayerAimSettings aimSettings;
    [SerializeField] private bool allowQuickShot = true;

    [Header("Quick Shot Rotation")]
    [SerializeField] private bool useQuickShotRotationSpeedOverride = true;
    [Min(0f)] [SerializeField] private float quickShotRotationSpeed = 22f;

    [Header("Quick Shot Timing")]
    [SerializeField] private bool waitForQuickShotFacing = true;
    [Min(0f)] [SerializeField] private float quickShotMinWaitTime = 0.03f;
    [Min(0f)] [SerializeField] private float quickShotMaxWaitTime = 0.12f;
    [Min(0f)] [SerializeField] private float quickShotFireAngle = 8f;

    [Header("Projectile - Real Logic")]
    [SerializeField] private BulletProjectile bulletProjectilePrefab;
    [SerializeField] private Transform projectileSpawnOverride;
    [Min(0.01f)] [SerializeField] private float bulletSpeed = 120f;
    [Min(0.01f)] [SerializeField] private float bulletMaxDistance = 50f;
    [Min(0.001f)] [SerializeField] private float bulletRadius = 0.04f;
    [SerializeField] private LayerMask bulletHitMask = ~0;
    [SerializeField] private QueryTriggerInteraction projectileTriggerInteraction = QueryTriggerInteraction.Collide;

    [Header("Projectile - Visual Only")]
    [SerializeField] private BulletProjectileVisual visualBulletProjectilePrefab;
    [SerializeField] private bool spawnVisualProjectile = true;
    [SerializeField] private Transform visualProjectileSpawnOverride;
    [Min(0.01f)] [SerializeField] private float visualBulletSpeed = 150f;

    [Header("Projectile - Visual Penetration")]
    [SerializeField] private bool visualPenetratesWhenWeaponPenetrates = true;
    [SerializeField] private bool forceVisualPenetration = false;
    [SerializeField] private LayerMask visualPenetrationStopMask = 0;
    [SerializeField] private QueryTriggerInteraction visualPenetrationTriggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("Fire Rate")]
    [Min(0f)] [SerializeField] private float shootCooldown = 0.12f;

    [Header("Spread")]
    [SerializeField] private bool useSpread = true;
    [SerializeField] private List<float> aimSpreadAngleList = new List<float> { 1.2f };
    [SerializeField] private List<float> quickShotSpreadAngleList = new List<float> { 4.0f };

    [Header("Recoil")]
    [SerializeField] private bool useRecoil = true;

    [Header("Animator")]
    [SerializeField] private string shootTriggerName = "Shoot";
    [SerializeField] private string quickShotBoolName = "QuickShot";
    [SerializeField] private string isShootingBoolName = "IsShooting";

    [Tooltip("Full-auto state. True while the player is holding fire and the weapon is continuously shooting.")]
    [SerializeField] private string keepShootingBoolName = "KeepShooting";

    [SerializeField] private bool resetTriggerBeforeSet = true;
    [SerializeField] private float isShootingHoldTime = 0.1f;

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
    private bool wasRealAimHeldLastFrame;

    private bool quickShotPendingFire;
    private float quickShotPendingStartTime;
    private bool quickShotFirstShotFired;

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
            damageSetting = GetComponent<AR_DamageSetting>();

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
            damageSetting = GetComponent<AR_DamageSetting>();

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
    }

    private void Update()
    {
        if (shootAction == null)
            return;

        bool shootHeld = shootAction.IsPressed();
        bool shootPressedThisFrame = shootAction.WasPressedThisFrame();
        bool shootReleasedThisFrame = shootAction.WasReleasedThisFrame();
        bool realAimHeldNow = aimSettings != null && aimSettings.IsAimInputHeld;

        if (shootReleasedThisFrame)
        {
            ClearQuickShotState();
            SetAnimatorKeepShootingBool(false);
        }

        if (shootPressedThisFrame && !realAimHeldNow && allowQuickShot && !quickShotRequiresRelease)
        {
            if (IsInShootCooldown())
                return;

            quickShotSessionActive = true;
            quickShotRequiresRelease = false;
            quickShotFirstShotFired = false;
            StartQuickShotPendingFire();
            ApplyQuickShotAimOverride();

            if (animator != null)
                animator.SetBool(quickShotBoolHash, true);
        }

        if (!wasRealAimHeldLastFrame && realAimHeldNow && shootHeld)
        {
            quickShotSessionActive = false;
            quickShotRequiresRelease = true;
            quickShotPendingFire = false;
            quickShotFirstShotFired = false;

            ClearQuickShotAimOverride();

            if (animator != null)
                animator.SetBool(quickShotBoolHash, false);
        }

        if (!realAimHeldNow && quickShotSessionActive && !quickShotRequiresRelease)
        {
            ApplyQuickShotAimOverride();

            if (animator != null)
                animator.SetBool(quickShotBoolHash, true);
        }

        bool canUseAimFire = shootHeld && realAimHeldNow;
        bool canUseQuickShotFire = shootHeld && quickShotSessionActive && !quickShotRequiresRelease;

        if (canUseQuickShotFire)
        {
            if (quickShotFirstShotFired)
            {
                Shoot();
            }
            else
            {
                if (!quickShotPendingFire)
                    StartQuickShotPendingFire();

                if (IsQuickShotReadyToFire())
                {
                    quickShotPendingFire = false;
                    Shoot();
                    quickShotFirstShotFired = true;
                }
            }
        }
        else if (canUseAimFire)
        {
            quickShotPendingFire = false;
            Shoot();
        }
        else
        {
            quickShotPendingFire = false;
            SetAnimatorKeepShootingBool(false);
        }

        wasRealAimHeldLastFrame = realAimHeldNow;
    }

    public void ForceClearRuntimeState()
    {
        quickShotSessionActive = false;
        quickShotRequiresRelease = false;
        wasRealAimHeldLastFrame = false;
        quickShotPendingFire = false;
        quickShotFirstShotFired = false;
        externalShootLock = false;

        ClearQuickShotAimOverride();

        if (animator != null)
        {
            animator.SetBool(isShootingBoolHash, false);
            animator.SetBool(quickShotBoolHash, false);
        }

        SetAnimatorKeepShootingBool(false);
    }

    public void Shoot()
    {
        if (IsInShootCooldown())
            return;

        if (externalShootLock)
        {
            StopContinuousShootingState();
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
                    StopContinuousShootingState();
                    return;
                }
            }
        }

        bool realAimHeldNow = aimSettings != null && aimSettings.IsAimInputHeld;
        bool isQuickShot = !realAimHeldNow && quickShotSessionActive && !quickShotRequiresRelease;
        bool isAimingShot = !isQuickShot;

        if (playerMovement != null)
        {
            if (isQuickShot)
            {
                if (!playerMovement.CanQuickShotNow)
                {
                    StopContinuousShootingState();
                    return;
                }
            }
            else
            {
                if (!playerMovement.CanAimShootNow)
                {
                    StopContinuousShootingState();
                    return;
                }
            }
        }

        if (isQuickShot && !allowQuickShot)
        {
            StopContinuousShootingState();
            return;
        }

        if (isQuickShot)
        {
            if (!quickShotSessionActive || quickShotRequiresRelease)
            {
                StopContinuousShootingState();
                return;
            }
        }

        if (MuzzlePointSettings == null || bulletProjectilePrefab == null)
        {
            StopContinuousShootingState();
            return;
        }

        if (WeaponAmmoSettings != null)
        {
            if (!WeaponAmmoSettings.TryConsumeOneRound())
            {
                StopContinuousShootingState();
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

        if (weaponEffects != null)
            weaponEffects.PlayGunshot();
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
        quickShotFirstShotFired = false;

        ClearQuickShotAimOverride();

        if (animator != null)
            animator.SetBool(quickShotBoolHash, false);
    }

    private void StopContinuousShootingState()
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
        quickShotFirstShotFired = false;

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
        float delay = Mathf.Max(0.01f, Mathf.Min(isShootingHoldTime, shootCooldown - 0.01f));

        yield return new WaitForSecondsRealtime(delay);

        if (animator != null)
            animator.SetBool(isShootingBoolHash, false);

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
}