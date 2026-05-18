using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemyWeaponShooter : MonoBehaviour
{
    public enum EnemyShootingType
    {
        AR,
        ShotGun
    }

    [Header("Shooting Type")]
    [SerializeField] private EnemyShootingType shootingType = EnemyShootingType.AR;

    [Header("Refs")]
    [SerializeField] private Transform muzzlePoint;
    [SerializeField] private Animator enemyAnimator;
    [SerializeField] private EnemyAnimatorParameterDriver enemyAnimatorDriver;
    [SerializeField] private EnemyWeaponSettings enemyWeaponSettings;

    [Header("Damage Setting - AR")]
    [SerializeField] private AR_DamageSetting damageSetting;

    [Header("Damage Setting - ShotGun")]
    [SerializeField] private SG_DamageSetting sgDamageSetting;

    [Header("Weapon SFX/VFX")]
    [SerializeField] private WeaponEffects weaponEffects;

    [Header("Projectile - Real Logic")]
    [SerializeField] private BulletProjectile bulletProjectilePrefab;
    [Min(0.01f)] [SerializeField] private float bulletSpeed = 120f;
    [Min(0.01f)] [SerializeField] private float bulletMaxDistance = 50f;
    [Min(0.001f)] [SerializeField] private float bulletRadius = 0.04f;
    [SerializeField] private LayerMask bulletHitMask = ~0;
    [SerializeField] private QueryTriggerInteraction projectileTriggerInteraction = QueryTriggerInteraction.Collide;

    [Header("Projectile - Visual Only")]
    [SerializeField] private BulletProjectileVisual visualBulletProjectilePrefab;
    [SerializeField] private bool spawnVisualProjectile = true;
    [SerializeField] private Transform visualProjectileSpawnPoint;
    [Min(0.01f)] [SerializeField] private float visualBulletSpeed = 150f;

    [Header("ShotGun Visual")]
    [SerializeField] private bool spawnVisualProjectileForEachPellet = false;

    [Header("Fire Rate")]
    [Min(0f)] [SerializeField] private float shootCooldown = 0.12f;

    [Header("Spread - AR / Default")]
    [SerializeField] private bool useSpread = true;
    [SerializeField] private List<float> spreadAngleList = new List<float> { 1.2f };

    [Header("Spread - ShotGun")]
    [Min(1)] [SerializeField] private int pelletCount = 6;
    [SerializeField] private List<float> shotGunSpreadAngleList = new List<float> { 5f, 6f, 7f };

    [Header("ShotGun Optional Impact")]
    [SerializeField] private bool spawnShotGunBulletImpact = false;
    [SerializeField] private BulletImpact bulletImpactPrefab;
    [SerializeField] private Transform bulletImpactSpawnPoint;
    [Min(0.01f)] [SerializeField] private float bulletImpactSpeed = 45f;
    [Min(0.01f)] [SerializeField] private float bulletImpactMaxDistance = 25f;
    [SerializeField] private LayerMask bulletImpactHitMask = ~0;

    [Header("Animator")]
    [SerializeField] private string shootTriggerName = "Shoot";
    [SerializeField] private string isShootingBoolName = "IsShooting";
    [SerializeField] private string keepShootingBoolName = "KeepShooting";
    [SerializeField] private bool resetTriggerBeforeSet = true;
    [SerializeField] private float isShootingHoldTime = 0.1f;

    [Header("Animator - Shooting Type")]
    [SerializeField] private bool useKeepShootingBoolForAR = true;
    [SerializeField] private bool useKeepShootingBoolForShotGun = false;

    [Header("External Locks")]
    public bool externalShootLock;

    [Header("Debug")]
    [SerializeField] private bool logShot = false;

    private float nextShootTime;

    private int shootTriggerHash;
    private int isShootingBoolHash;
    private int keepShootingBoolHash;

    private Coroutine shootingBoolRoutine;

    private void Awake()
    {
        ResolveReferences();

        shootTriggerHash = Animator.StringToHash(shootTriggerName);
        isShootingBoolHash = Animator.StringToHash(isShootingBoolName);
        keepShootingBoolHash = Animator.StringToHash(keepShootingBoolName);
    }

    private void OnDisable()
    {
        ForceClearRuntimeState();
    }

    private void ResolveReferences()
    {
        Transform root = transform.root;

        if (muzzlePoint == null)
            muzzlePoint = transform;

        if (enemyAnimator == null)
            enemyAnimator = root.GetComponentInChildren<Animator>();

        if (enemyAnimatorDriver == null)
            enemyAnimatorDriver = root.GetComponent<EnemyAnimatorParameterDriver>();

        if (enemyWeaponSettings == null)
            enemyWeaponSettings = GetComponent<EnemyWeaponSettings>();

        if (enemyWeaponSettings == null)
            enemyWeaponSettings = GetComponentInChildren<EnemyWeaponSettings>(true);

        if (damageSetting == null)
            damageSetting = GetComponent<AR_DamageSetting>();

        if (damageSetting == null)
            damageSetting = GetComponentInChildren<AR_DamageSetting>(true);

        if (sgDamageSetting == null)
            sgDamageSetting = GetComponent<SG_DamageSetting>();

        if (sgDamageSetting == null)
            sgDamageSetting = GetComponentInChildren<SG_DamageSetting>(true);

        if (weaponEffects == null)
            weaponEffects = GetComponentInChildren<WeaponEffects>(true);
    }

    public bool CanShoot()
    {
        if (externalShootLock)
            return false;

        if (shootCooldown > 0f && Time.time < nextShootTime)
            return false;

        if (bulletProjectilePrefab == null || muzzlePoint == null)
            return false;

        if (enemyWeaponSettings != null && !enemyWeaponSettings.CanShoot())
            return false;

        return true;
    }

    public bool NeedsReload()
    {
        if (enemyWeaponSettings == null)
            return false;

        return enemyWeaponSettings.IsMagazineEmpty && enemyWeaponSettings.HasReserveAmmo;
    }

    public bool IsReloading()
    {
        if (enemyWeaponSettings == null)
            return false;

        return enemyWeaponSettings.IsReloading;
    }

    public EnemyWeaponSettings GetEnemyWeaponSettings()
    {
        return enemyWeaponSettings;
    }

    public bool ShootAt(Vector3 targetPoint)
    {
        if (!CanShoot())
        {
            StopContinuousShootingState();
            return false;
        }

        if (enemyWeaponSettings != null && !enemyWeaponSettings.TryConsumeOneRound())
        {
            StopContinuousShootingState();
            return false;
        }

        nextShootTime = Time.time + Mathf.Max(0f, shootCooldown);

        Vector3 origin = muzzlePoint.position;
        Vector3 baseDir = targetPoint - origin;

        if (baseDir.sqrMagnitude <= 0.0001f)
            baseDir = muzzlePoint.forward;

        baseDir.Normalize();

        switch (shootingType)
        {
            case EnemyShootingType.ShotGun:
                ShootShotGun(origin, baseDir);
                break;

            case EnemyShootingType.AR:
            default:
                ShootAR(origin, baseDir);
                break;
        }

        if (weaponEffects != null)
            weaponEffects.PlayGunshot();

        PlayShootAnimator();

        if (logShot)
        {
            Debug.Log(
                $"[EnemyWeaponShooter] ShootAt. Type={shootingType}, Origin={origin}, Target={targetPoint}, BaseDir={baseDir}",
                this
            );
        }

        return true;
    }

    public void ForceClearRuntimeState()
    {
        externalShootLock = false;
        StopContinuousShootingState();
    }

    private void ShootAR(Vector3 origin, Vector3 baseDir)
    {
        Vector3 dir = ApplySpread(baseDir, GetRandomSpreadAngle(spreadAngleList));

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

        SpawnVisualProjectile(origin, dir);
    }

    private void ShootShotGun(Vector3 origin, Vector3 baseDir)
    {
        int count = Mathf.Max(1, pelletCount);
        Vector3 firstPelletDir = baseDir;

        for (int i = 0; i < count; i++)
        {
            Vector3 pelletDir = ApplySpread(baseDir, GetRandomSpreadAngle(shotGunSpreadAngleList));

            if (i == 0)
                firstPelletDir = pelletDir;

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

            if (sgDamageSetting != null)
                sgDamageSetting.ApplyToProjectile(bullet);
            else if (damageSetting != null)
                damageSetting.ApplyToProjectile(bullet);

            if (spawnVisualProjectileForEachPellet)
                SpawnVisualProjectile(origin, pelletDir);
        }

        if (!spawnVisualProjectileForEachPellet)
            SpawnVisualProjectile(origin, firstPelletDir);

        if (spawnShotGunBulletImpact)
            SpawnShotGunBulletImpact(origin, baseDir);
    }

    private void SpawnShotGunBulletImpact(Vector3 shotOrigin, Vector3 shotBaseDir)
    {
        if (bulletImpactPrefab == null)
            return;

        Vector3 spawnOrigin = bulletImpactSpawnPoint != null
            ? bulletImpactSpawnPoint.position
            : shotOrigin;

        Vector3 dir = shotBaseDir;

        if (dir.sqrMagnitude <= 0.0001f)
            dir = muzzlePoint != null ? muzzlePoint.forward : transform.forward;

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

    private void PlayShootAnimator()
    {
        bool useKeepShooting = ShouldUseKeepShootingBool();

        if (enemyAnimatorDriver != null)
        {
            enemyAnimatorDriver.SetShooting(true);
            enemyAnimatorDriver.SetKeepShooting(useKeepShooting);
            enemyAnimatorDriver.TriggerShoot();
        }
        else if (enemyAnimator != null)
        {
            if (resetTriggerBeforeSet)
                enemyAnimator.ResetTrigger(shootTriggerHash);

            enemyAnimator.SetTrigger(shootTriggerHash);
            enemyAnimator.SetBool(isShootingBoolHash, true);

            if (!string.IsNullOrWhiteSpace(keepShootingBoolName))
                enemyAnimator.SetBool(keepShootingBoolHash, useKeepShooting);
        }

        if (shootingBoolRoutine != null)
            StopCoroutine(shootingBoolRoutine);

        shootingBoolRoutine = StartCoroutine(ResetShootingAfterDelay());
    }

    private bool ShouldUseKeepShootingBool()
    {
        switch (shootingType)
        {
            case EnemyShootingType.ShotGun:
                return useKeepShootingBoolForShotGun;

            case EnemyShootingType.AR:
            default:
                return useKeepShootingBoolForAR;
        }
    }

    private IEnumerator ResetShootingAfterDelay()
    {
        float delay = Mathf.Max(0.01f, Mathf.Min(isShootingHoldTime, shootCooldown - 0.01f));

        if (shootCooldown <= 0.01f)
            delay = isShootingHoldTime;

        yield return new WaitForSeconds(delay);

        StopContinuousShootingState();
        shootingBoolRoutine = null;
    }

    private void StopContinuousShootingState()
    {
        if (enemyAnimatorDriver != null)
        {
            enemyAnimatorDriver.SetShooting(false);
            enemyAnimatorDriver.SetKeepShooting(false);
        }
        else if (enemyAnimator != null)
        {
            enemyAnimator.SetBool(isShootingBoolHash, false);

            if (!string.IsNullOrWhiteSpace(keepShootingBoolName))
                enemyAnimator.SetBool(keepShootingBoolHash, false);
        }
    }

    private float GetRandomSpreadAngle(List<float> list)
    {
        if (!useSpread)
            return 0f;

        if (list == null || list.Count == 0)
            return 0f;

        int index = Random.Range(0, list.Count);
        return Mathf.Max(0f, list[index]);
    }

    private Vector3 ApplySpread(Vector3 baseDir, float spreadAngleDeg)
    {
        baseDir = baseDir.sqrMagnitude > 0.0001f ? baseDir.normalized : transform.forward;

        if (!useSpread || spreadAngleDeg <= 0.001f)
            return baseDir;

        Quaternion basis = Quaternion.LookRotation(baseDir, Vector3.up);

        float tan = Mathf.Tan(spreadAngleDeg * Mathf.Deg2Rad);
        Vector2 offset = Random.insideUnitCircle * tan;

        Vector3 localDir = new Vector3(offset.x, offset.y, 1f).normalized;
        return (basis * localDir).normalized;
    }

    private void SpawnVisualProjectile(Vector3 origin, Vector3 dir)
    {
        if (!spawnVisualProjectile || visualBulletProjectilePrefab == null)
            return;

        Vector3 visualOrigin = visualProjectileSpawnPoint != null
            ? visualProjectileSpawnPoint.position
            : origin;

        Vector3 targetPoint;

        if (Physics.Raycast(
            origin,
            dir,
            out RaycastHit hit,
            bulletMaxDistance,
            bulletHitMask,
            projectileTriggerInteraction))
        {
            targetPoint = hit.point;
        }
        else
        {
            targetPoint = origin + dir * bulletMaxDistance;
        }

        BulletProjectileVisual visualBullet =
            Instantiate(visualBulletProjectilePrefab, visualOrigin, Quaternion.identity);

        visualBullet.Init(visualOrigin, targetPoint, visualBulletSpeed);
    }
}