using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class PlayerShootSettings : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private MuzzlePointSettings muzzlePointSettings;
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerCrossHairSettings crosshairSettings;
    [SerializeField] private CameraNoiseByMovement cameraNoiseByMovement;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private AssaultRifleAmmoSettings assaultRifleAmmoSettings;

    [Header("Weapon SFX/VFX")]
    [SerializeField] private WeaponEffects weaponEffects;

    [Header("Aim Gate")]
    [SerializeField] private PlayerAimSettings aimSettings;
    [SerializeField] private bool allowQuickShot = true;

    [Header("Projectile (real logic)")]
    [SerializeField] private BulletProjectile bulletProjectilePrefab;
    [SerializeField] private Transform projectileSpawnOverride;
    [Min(0.01f)] [SerializeField] private float bulletSpeed = 120f;
    [Min(0.01f)] [SerializeField] private float bulletMaxDistance = 50f;
    [Min(0.001f)] [SerializeField] private float bulletRadius = 0.04f;
    [SerializeField] private LayerMask bulletHitMask = ~0;
    [SerializeField] private QueryTriggerInteraction projectileTriggerInteraction = QueryTriggerInteraction.Collide;

    [Header("Projectile (visual only)")]
    [SerializeField] private BulletProjectileVisual visualBulletProjectilePrefab;
    [SerializeField] private bool spawnVisualProjectile = true;
    [SerializeField] private Transform visualProjectileSpawnOverride;
    [Min(0.01f)] [SerializeField] private float visualBulletSpeed = 150f;

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
    [SerializeField] private bool resetTriggerBeforeSet = true;
    [SerializeField] private float isShootingHoldTime = 0.1f;

    [Header("Input (optional)")]
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

    private bool quickShotSessionActive;
    private bool quickShotRequiresRelease;
    private bool wasRealAimHeldLastFrame;

    private void Reset()
    {
        if (aimSettings == null) aimSettings = GetComponent<PlayerAimSettings>();
        if (playerMovement == null) playerMovement = GetComponent<PlayerMovement>();
        if (playerInput == null) playerInput = GetComponent<PlayerInput>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (crosshairSettings == null) crosshairSettings = FindFirstObjectByType<PlayerCrossHairSettings>();
        if (weaponEffects == null) weaponEffects = GetComponentInChildren<WeaponEffects>();
        if (assaultRifleAmmoSettings == null) assaultRifleAmmoSettings = GetComponent<AssaultRifleAmmoSettings>();
    }

    private void Awake()
    {
        if (aimSettings == null) aimSettings = GetComponent<PlayerAimSettings>();
        if (playerMovement == null) playerMovement = GetComponent<PlayerMovement>();
        if (playerInput == null) playerInput = GetComponent<PlayerInput>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (weaponEffects == null) weaponEffects = GetComponentInChildren<WeaponEffects>();
        if (assaultRifleAmmoSettings == null) assaultRifleAmmoSettings = GetComponent<AssaultRifleAmmoSettings>();

        if (playerInput != null && playerInput.actions != null)
            shootAction = playerInput.actions[shootActionName];

        shootTriggerHash = Animator.StringToHash(shootTriggerName);
        quickShotBoolHash = Animator.StringToHash(quickShotBoolName);
        isShootingBoolHash = Animator.StringToHash(isShootingBoolName);
        nextShootTime = 0f;
    }

    private void OnEnable()
    {
        if (shootAction != null)
            shootAction.Enable();

        quickShotSessionActive = false;
        quickShotRequiresRelease = false;
        wasRealAimHeldLastFrame = false;

        if (aimSettings != null)
            aimSettings.SetExternalAimOverride(false);

        if (animator != null)
        {
            animator.SetBool(isShootingBoolHash, false);
            animator.SetBool(quickShotBoolHash, false);
        }
    }

    private void OnDisable()
    {
        if (shootAction != null)
            shootAction.Disable();

        if (shootingBoolRoutine != null)
        {
            StopCoroutine(shootingBoolRoutine);
            shootingBoolRoutine = null;
        }

        quickShotSessionActive = false;
        quickShotRequiresRelease = false;
        wasRealAimHeldLastFrame = false;

        if (aimSettings != null)
            aimSettings.SetExternalAimOverride(false);

        if (animator != null)
        {
            animator.SetBool(isShootingBoolHash, false);
            animator.SetBool(quickShotBoolHash, false);
        }
    }

    private void Update()
    {
        if (shootAction == null)
            return;

        if (assaultRifleAmmoSettings != null && assaultRifleAmmoSettings.IsReloading)
        {
            quickShotSessionActive = false;
            quickShotRequiresRelease = true;

            if (aimSettings != null)
                aimSettings.SetExternalAimOverride(false);

            if (animator != null)
            {
                animator.SetBool(quickShotBoolHash, false);
                animator.SetBool(isShootingBoolHash, false);
            }

            return;
        }

        bool shootHeld = shootAction.IsPressed();
        bool shootPressedThisFrame = shootAction.WasPressedThisFrame();
        bool shootReleasedThisFrame = shootAction.WasReleasedThisFrame();
        bool realAimHeldNow = aimSettings != null && aimSettings.IsAimInputHeld;

        if (shootReleasedThisFrame)
        {
            quickShotSessionActive = false;
            quickShotRequiresRelease = false;

            if (aimSettings != null)
                aimSettings.SetExternalAimOverride(false);

            if (animator != null)
                animator.SetBool(quickShotBoolHash, false);
        }

        if (shootPressedThisFrame && !realAimHeldNow && allowQuickShot && !quickShotRequiresRelease)
        {
            quickShotSessionActive = true;

            if (aimSettings != null)
                aimSettings.SetExternalAimOverride(true);

            if (animator != null)
                animator.SetBool(quickShotBoolHash, true);
        }

        if (!wasRealAimHeldLastFrame && realAimHeldNow && shootHeld)
        {
            quickShotSessionActive = false;
            quickShotRequiresRelease = true;

            if (aimSettings != null)
                aimSettings.SetExternalAimOverride(false);

            if (animator != null)
                animator.SetBool(quickShotBoolHash, false);
        }

        if (!realAimHeldNow && quickShotSessionActive && !quickShotRequiresRelease)
        {
            if (aimSettings != null)
                aimSettings.SetExternalAimOverride(true);

            if (animator != null)
                animator.SetBool(quickShotBoolHash, true);
        }

        if (shootHeld)
        {
            if (realAimHeldNow)
            {
                Shoot();
            }
            else if (quickShotSessionActive && !quickShotRequiresRelease)
            {
                Shoot();
            }
        }

        wasRealAimHeldLastFrame = realAimHeldNow;
    }

    public void Shoot()
    {
        if (shootCooldown > 0f && Time.time < nextShootTime)
            return;

        if (externalShootLock)
            return;

        if (playerMovement != null && !playerMovement.CanShootNow)
            return;

        if (assaultRifleAmmoSettings != null)
        {
            if (!assaultRifleAmmoSettings.CanShoot())
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

        if (muzzlePointSettings == null || bulletProjectilePrefab == null)
            return;

        if (assaultRifleAmmoSettings != null)
        {
            if (!assaultRifleAmmoSettings.TryConsumeOneRound())
                return;
        }

        nextShootTime = Time.time + Mathf.Max(0f, shootCooldown);

        if (crosshairSettings != null && useRecoil)
        {
            if (isAimingShot)
                crosshairSettings.AddAimShotRecoil();
            else if (isQuickShot)
                crosshairSettings.AddQuickShotRecoil();
        }

        if (animator != null)
        {
            if (resetTriggerBeforeSet)
                animator.ResetTrigger(shootTriggerHash);

            if (isQuickShot)
                animator.SetBool(quickShotBoolHash, true);
            else
                animator.SetBool(quickShotBoolHash, false);

            animator.SetTrigger(shootTriggerHash);
            animator.SetBool(isShootingBoolHash, true);

            if (shootingBoolRoutine != null)
                StopCoroutine(shootingBoolRoutine);

            shootingBoolRoutine = StartCoroutine(ResetIsShootingAfterDelay());
        }

        muzzlePointSettings.RequestDebugDraw();

        Vector3 origin = projectileSpawnOverride != null
            ? projectileSpawnOverride.position
            : muzzlePointSettings.LastRay.origin;

        Vector3 targetPoint = ResolveShotTargetPoint(origin, isAimingShot);

        Vector3 dir = targetPoint - origin;
        if (dir.sqrMagnitude <= 0.0001f)
            dir = muzzlePointSettings.LastRay.direction;

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

        SpawnVisualProjectile(shotRay);

        if (weaponEffects != null)
            weaponEffects.PlayGunshot();
    }

    private Vector3 ResolveShotTargetPoint(Vector3 origin, bool isAimingShot)
    {
        if (isAimingShot && muzzlePointSettings != null)
            return muzzlePointSettings.AimPoint;

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

        if (crosshairSettings != null)
        {
            if (crosshairSettings.mouseAimLayers.value != 0 &&
                Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    crosshairSettings.mouseRayMaxDistance,
                    crosshairSettings.mouseAimLayers,
                    QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
                return true;
            }

            if (crosshairSettings.mouseGroundLayers.value != 0 &&
                Physics.Raycast(
                    ray,
                    out RaycastHit groundHit,
                    crosshairSettings.mouseRayMaxDistance,
                    crosshairSettings.mouseGroundLayers,
                    QueryTriggerInteraction.Ignore))
            {
                point = groundHit.point;
                return true;
            }

            point = ray.origin + ray.direction * crosshairSettings.mouseRayMaxDistance;
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

        yield return new WaitForSeconds(delay);

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
        if (Physics.Raycast(
            shotRay,
            out RaycastHit hit,
            bulletMaxDistance,
            bulletHitMask,
            projectileTriggerInteraction))
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
}