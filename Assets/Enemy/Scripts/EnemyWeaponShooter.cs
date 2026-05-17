using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemyWeaponShooter : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform muzzlePoint;
    [SerializeField] private Animator enemyAnimator;
    [SerializeField] private EnemyAnimatorParameterDriver enemyAnimatorDriver;
    [SerializeField] private EnemyWeaponSettings enemyWeaponSettings;
    [SerializeField] private AR_DamageSetting damageSetting;
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

    [Header("Fire Rate")]
    [Min(0f)] [SerializeField] private float shootCooldown = 0.12f;

    [Header("Spread")]
    [SerializeField] private bool useSpread = true;
    [SerializeField] private List<float> spreadAngleList = new List<float> { 1.2f };

    [Header("Animator")]
    [SerializeField] private string shootTriggerName = "Shoot";
    [SerializeField] private string isShootingBoolName = "IsShooting";
    [SerializeField] private string keepShootingBoolName = "KeepShooting";
    [SerializeField] private bool resetTriggerBeforeSet = true;
    [SerializeField] private float isShootingHoldTime = 0.1f;

    [Header("External Locks")]
    public bool externalShootLock;

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
        Vector3 dir = targetPoint - origin;

        if (dir.sqrMagnitude <= 0.0001f)
            dir = muzzlePoint.forward;

        dir.Normalize();
        dir = ApplySpread(dir, GetRandomSpreadAngle());

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

        if (weaponEffects != null)
            weaponEffects.PlayGunshot();

        PlayShootAnimator();

        return true;
    }

    public void ForceClearRuntimeState()
    {
        externalShootLock = false;
        StopContinuousShootingState();
    }

    private void PlayShootAnimator()
    {
        if (enemyAnimatorDriver != null)
        {
            enemyAnimatorDriver.SetShooting(true);
            enemyAnimatorDriver.SetKeepShooting(true);
            enemyAnimatorDriver.TriggerShoot();
        }
        else if (enemyAnimator != null)
        {
            if (resetTriggerBeforeSet)
                enemyAnimator.ResetTrigger(shootTriggerHash);

            enemyAnimator.SetTrigger(shootTriggerHash);
            enemyAnimator.SetBool(isShootingBoolHash, true);
            enemyAnimator.SetBool(keepShootingBoolHash, true);
        }

        if (shootingBoolRoutine != null)
            StopCoroutine(shootingBoolRoutine);

        shootingBoolRoutine = StartCoroutine(ResetShootingAfterDelay());
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
            enemyAnimator.SetBool(keepShootingBoolHash, false);
        }
    }

    private float GetRandomSpreadAngle()
    {
        if (!useSpread)
            return 0f;

        if (spreadAngleList == null || spreadAngleList.Count == 0)
            return 0f;

        int index = Random.Range(0, spreadAngleList.Count);
        return Mathf.Max(0f, spreadAngleList[index]);
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