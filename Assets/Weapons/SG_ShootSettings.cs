using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class SG_ShootSettings : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private MuzzlePointSettings MuzzlePointSettings;
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerCrossHairSettings PlayerCrossHairSettings;
    [SerializeField] private CameraNoiseByMovement cameraNoiseByMovement;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private WeaponAmmoSettings WeaponAmmoSettings;

    [Header("Damage Setting")]
    [SerializeField] private SG_DamageSetting damageSetting;

    [Header("Weapon SFX/VFX")]
    [SerializeField] private WeaponEffects weaponEffects;

    [Header("Aim Gate")]
    [SerializeField] private PlayerAimSettings aimSettings;
    [SerializeField] private bool allowQuickShot = true;

    [Header("Quick Shot Rotation")]
    [SerializeField] private bool useQuickShotRotationSpeedOverride = true;
    [Min(0f)] [SerializeField] private float quickShotRotationSpeed = 14f;

    [Header("Quick Shot Timing")]
    [SerializeField] private bool useQuickShotShootDelay = false;

    [Tooltip("Delay before the real shotgun blast happens in quick shot mode. Used to match animation timing.")]
    [Min(0f)] [SerializeField] private float quickShotShootDelay = 0.12f;

    [Tooltip("If true, releasing Shoot before the delay ends will cancel the delayed quick shot.")]
    [SerializeField] private bool cancelDelayedQuickShotOnRelease = true;

    [Header("Quick Shot Facing Gate")]
    [SerializeField] private bool waitForQuickShotFacing = true;
    [Min(0f)] [SerializeField] private float quickShotMinWaitTime = 0.03f;
    [Min(0f)] [SerializeField] private float quickShotMaxWaitTime = 0.12f;
    [Min(0f)] [SerializeField] private float quickShotFireAngle = 8f;

    [Header("Projectile - Real Logic")]
    [SerializeField] private BulletProjectile bulletProjectilePrefab;
    [SerializeField] private Transform projectileSpawnOverride;
    [Min(0.01f)] [SerializeField] private float bulletSpeed = 120f;
    [Min(0.01f)] [SerializeField] private float bulletMaxDistance = 35f;
    [Min(0.001f)] [SerializeField] private float bulletRadius = 0.04f;
    [SerializeField] private LayerMask bulletHitMask = ~0;
    [SerializeField] private QueryTriggerInteraction projectileTriggerInteraction = QueryTriggerInteraction.Collide;

    [Header("Shotgun Bullet Impact")]
    [SerializeField] private bool spawnBulletImpact = true;
    [SerializeField] private BulletImpact bulletImpactPrefab;
    [SerializeField] private Transform bulletImpactSpawnOverride;
    [Min(0.01f)] [SerializeField] private float bulletImpactSpeed = 45f;
    [Min(0.01f)] [SerializeField] private float bulletImpactMaxDistance = 25f;
    [SerializeField] private LayerMask bulletImpactHitMask = ~0;

    [Header("Projectile - Visual Only")]
    [SerializeField] private BulletProjectileVisual visualBulletProjectilePrefab;
    [SerializeField] private bool spawnVisualProjectile = true;
    [SerializeField] private bool spawnVisualProjectileForEachPellet = true;
    [SerializeField] private Transform visualProjectileSpawnOverride;
    [Min(0.01f)] [SerializeField] private float visualBulletSpeed = 150f;

    [Header("Projectile - Visual Penetration")]
    [SerializeField] private bool visualPenetratesWhenWeaponPenetrates = true;
    [SerializeField] private bool forceVisualPenetration = false;
    [SerializeField] private LayerMask visualPenetrationStopMask = 0;
    [SerializeField] private QueryTriggerInteraction visualPenetrationTriggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("Fire Rate")]
    [Min(0f)] [SerializeField] private float shootCooldown = 0.75f;

    [Header("Shotgun Pellets")]
    [Min(1)] [SerializeField] private int pelletCount = 6;

    [Tooltip("False = one shotgun blast consumes one shell. True = each pellet consumes one ammo.")]
    [SerializeField] private bool consumeAmmoPerPellet = false;

    [Header("Spread")]
    [SerializeField] private bool useSpread = true;
    [SerializeField] private List<float> aimSpreadAngleList = new List<float> { 5.0f };
    [SerializeField] private List<float> quickShotSpreadAngleList = new List<float> { 9.0f };

    [Header("Recoil")]
    [SerializeField] private bool useRecoil = true;

    [Header("Animator")]
    [SerializeField] private string shootTriggerName = "Shoot";
    [SerializeField] private string quickShotBoolName = "QuickShot";
    [SerializeField] private string isShootingBoolName = "IsShooting";
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

    private Coroutine shootingBoolRoutine;
    private Coroutine quickShotDelayRoutine;

    private bool quickShotSessionActive;
    private bool quickShotRequiresRelease;
    private bool wasRealAimHeldLastFrame;

    private bool quickShotPendingFire;
    private float quickShotPendingStartTime;
    private bool quickShotDelayFinished;

    private void Reset()
    {
        Transform root = transform.root;

        if (MuzzlePointSettings == null)
            MuzzlePointSettings = GetComponent<MuzzlePointSettings>();

        if (PlayerCrossHairSettings == null)
            PlayerCrossHairSettings = GetComponent<PlayerCrossHairSettings>();

        if (WeaponAmmoSettings == null)
            WeaponAmmoSettings = GetComponent<WeaponAmmoSettings>();

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

        if (damageSetting == null)
            damageSetting = GetComponent<SG_DamageSetting>();
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

        if (damageSetting == null)
            damageSetting = GetComponent<SG_DamageSetting>();

        shootTriggerHash = Animator.StringToHash(shootTriggerName);
        quickShotBoolHash = Animator.StringToHash(quickShotBoolName);
        isShootingBoolHash = Animator.StringToHash(isShootingBoolName);

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

        if (quickShotDelayRoutine != null)
        {
            StopCoroutine(quickShotDelayRoutine);
            quickShotDelayRoutine = null;
        }

        ForceClearRuntimeState();
    }

    private void Update()
    {
        if (shootAction == null)
            return;

        bool shootPressedThisFrame = shootAction.WasPressedThisFrame();
        bool shootReleasedThisFrame = shootAction.WasReleasedThisFrame();
        bool realAimHeldNow = aimSettings != null && aimSettings.IsAimInputHeld;

        if (shootReleasedThisFrame)
        {
            if (quickShotDelayRoutine != null && cancelDelayedQuickShotOnRelease)
            {
                StopCoroutine(quickShotDelayRoutine);
                quickShotDelayRoutine = null;
                ClearQuickShotState();
            }
            else if (quickShotDelayRoutine == null)
            {
                ClearQuickShotState();
            }
        }

        if (shootPressedThisFrame && !realAimHeldNow && allowQuickShot && !quickShotRequiresRelease)
        {
            if (IsInShootCooldown())
                return;

            quickShotSessionActive = true;
            quickShotRequiresRelease = false;
            quickShotDelayFinished = !useQuickShotShootDelay || quickShotShootDelay <= 0f;

            ApplyQuickShotAimOverride();

            if (animator != null)
                animator.SetBool(quickShotBoolHash, true);

            StartQuickShotPendingFire();

            if (useQuickShotShootDelay && quickShotShootDelay > 0f)
            {
                if (quickShotDelayRoutine != null)
                    StopCoroutine(quickShotDelayRoutine);

                quickShotDelayRoutine = StartCoroutine(DelayedQuickShotRoutine());
            }
        }
        else if (shootPressedThisFrame && realAimHeldNow)
        {
            if (quickShotDelayRoutine != null)
            {
                StopCoroutine(quickShotDelayRoutine);
                quickShotDelayRoutine = null;
            }

            ClearQuickShotState();
            Shoot();
        }

        if (quickShotSessionActive && !quickShotRequiresRelease)
            ApplyQuickShotAimOverride();

        ProcessPendingQuickShotFire();

        wasRealAimHeldLastFrame = realAimHeldNow;
    }

    private IEnumerator DelayedQuickShotRoutine()
    {
        yield return new WaitForSecondsRealtime(quickShotShootDelay);

        quickShotDelayRoutine = null;
        quickShotDelayFinished = true;
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

        if (!quickShotDelayFinished)
            return;

        if (cancelDelayedQuickShotOnRelease && shootAction != null && !shootAction.IsPressed())
        {
            ClearQuickShotState();
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

        if (shootAction == null || !shootAction.IsPressed())
            ClearQuickShotState();
    }

    public void ForceClearRuntimeState()
    {
        if (quickShotDelayRoutine != null)
        {
            StopCoroutine(quickShotDelayRoutine);
            quickShotDelayRoutine = null;
        }

        quickShotSessionActive = false;
        quickShotRequiresRelease = false;
        wasRealAimHeldLastFrame = false;
        quickShotPendingFire = false;
        quickShotDelayFinished = false;
        externalShootLock = false;

        ClearQuickShotAimOverride();

        if (animator != null)
        {
            animator.SetBool(isShootingBoolHash, false);
            animator.SetBool(quickShotBoolHash, false);
        }
    }

    private void ClearQuickShotState()
    {
        if (quickShotDelayRoutine != null)
        {
            StopCoroutine(quickShotDelayRoutine);
            quickShotDelayRoutine = null;
        }

        quickShotSessionActive = false;
        quickShotRequiresRelease = false;
        quickShotPendingFire = false;
        quickShotDelayFinished = false;

        ClearQuickShotAimOverride();

        if (animator != null)
            animator.SetBool(quickShotBoolHash, false);
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

    private bool IsInShootCooldown()
    {
        return shootCooldown > 0f && Time.unscaledTime < nextShootTime;
    }

    public void Shoot()
    {
        if (IsInShootCooldown())
            return;

        if (externalShootLock)
            return;

        if (playerMovement != null && !playerMovement.CanShootNow)
            return;

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

            if (!CanConsumeRequiredAmmo())
                return;
        }

        bool realAimHeldNow = aimSettings != null && aimSettings.IsAimInputHeld;
        bool isQuickShot = !realAimHeldNow && quickShotSessionActive && !quickShotRequiresRelease;
        bool isAimingShot = !isQuickShot;

        if (isQuickShot && !allowQuickShot)
            return;

        if (isQuickShot)
        {
            if (!quickShotSessionActive || quickShotRequiresRelease)
                return;
        }

        if (MuzzlePointSettings == null || bulletProjectilePrefab == null)
            return;

        if (WeaponAmmoSettings != null)
        {
            if (!ConsumeRequiredAmmo())
                return;
        }

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
        Vector3 baseDir;

        if (TryGetViewShotRay(out Ray viewShotRay))
        {
            origin = viewShotRay.origin;
            baseDir = viewShotRay.direction;
        }
        else
        {
            origin = projectileSpawnOverride != null
                ? projectileSpawnOverride.position
                : MuzzlePointSettings.LastRay.origin;

            Vector3 targetPoint = ResolveShotTargetPoint(origin, isAimingShot);

            baseDir = targetPoint - origin;
            if (baseDir.sqrMagnitude <= 0.0001f)
                baseDir = MuzzlePointSettings.LastRay.direction;
        }

        baseDir.Normalize();

        FirePellets(origin, baseDir, isAimingShot);

        if (spawnBulletImpact)
            FireBulletImpact(origin, baseDir);

        if (weaponEffects != null)
            weaponEffects.PlayGunshot();
    }

    private void FirePellets(Vector3 origin, Vector3 baseDir, bool isAimingShot)
    {
        int count = Mathf.Max(1, pelletCount);

        Ray firstPelletRay = new Ray(origin, baseDir);

        for (int i = 0; i < count; i++)
        {
            float spreadAngle = GetRandomSpreadAngle(isAimingShot);
            Vector3 pelletDir = ApplySpread(baseDir, spreadAngle);

            Ray pelletRay = new Ray(origin, pelletDir);

            BulletProjectile bullet = Instantiate(bulletProjectilePrefab, origin, Quaternion.identity);
            bullet.Init(
                origin,
                pelletDir,
                bulletSpeed,
                bulletMaxDistance,
                bulletRadius,
                bulletHitMask,
                projectileTriggerInteraction
            );

            if (damageSetting != null)
                damageSetting.ApplyToProjectile(bullet);

            if (i == 0)
                firstPelletRay = pelletRay;

            if (spawnVisualProjectileForEachPellet)
                SpawnVisualProjectile(pelletRay);
        }

        if (!spawnVisualProjectileForEachPellet)
            SpawnVisualProjectile(firstPelletRay);
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

    private void ClearShootStateWhenReloadBlocksShot()
    {
        quickShotSessionActive = false;
        quickShotRequiresRelease = true;
        quickShotPendingFire = false;
        quickShotDelayFinished = false;

        ClearQuickShotAimOverride();

        if (animator != null)
        {
            animator.SetBool(quickShotBoolHash, false);
            animator.SetBool(isShootingBoolHash, false);
        }
    }

    private bool CanConsumeRequiredAmmo()
    {
        if (WeaponAmmoSettings == null)
            return true;

        int requiredAmmo = GetRequiredAmmoPerShot();

        if (requiredAmmo <= 0)
            return true;

        return WeaponAmmoSettings.CurrentAmmoInMagazine >= requiredAmmo;
    }

    private bool ConsumeRequiredAmmo()
    {
        if (WeaponAmmoSettings == null)
            return true;

        int requiredAmmo = GetRequiredAmmoPerShot();

        for (int i = 0; i < requiredAmmo; i++)
        {
            if (!WeaponAmmoSettings.TryConsumeOneRound())
                return false;
        }

        return true;
    }

    private int GetRequiredAmmoPerShot()
    {
        if (consumeAmmoPerPellet)
            return Mathf.Max(1, pelletCount);

        return 1;
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
            visualOrigin = shotRay.origin;
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