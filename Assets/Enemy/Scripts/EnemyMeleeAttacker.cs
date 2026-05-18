using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemyMeleeAttacker : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform attackOrigin;
    [SerializeField] private Animator enemyAnimator;
    [SerializeField] private EnemyAnimatorParameterDriver enemyAnimatorDriver;
    [SerializeField] private EnemyWeaponSettings enemyWeaponSettings;
    [SerializeField] private WeaponEffects weaponEffects;

    [Header("Melee Range")]
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float attackAngle = 120f;

    [Header("Fire Rate / Cooldown")]
    [Min(0f)] [SerializeField] private float shootCooldown = 1.2f;

    [Header("Facing")]
    [SerializeField] private bool faceTargetBeforeAttack = true;
    [SerializeField] private bool snapFaceTarget = true;
    [SerializeField] private float faceTurnSpeedDegrees = 720f;

    [Header("Animator")]
    [SerializeField] private string shootTriggerName = "Shoot";
    [SerializeField] private string isShootingBoolName = "IsShooting";
    [SerializeField] private string keepShootingBoolName = "KeepShooting";
    [SerializeField] private bool resetTriggerBeforeSet = true;
    [SerializeField] private bool useKeepShootingBool = false;
    [SerializeField] private float isShootingHoldTime = 0.1f;

    [Header("Animator Driver")]
    [SerializeField] private bool useEnemyAnimatorDriver = true;

    [Header("SFX/VFX")]
    [SerializeField] private bool playWeaponEffectsOnAttack = false;

    [Header("External Locks")]
    public bool externalShootLock;

    [Header("Debug")]
    [SerializeField] private bool logShot = false;
    [SerializeField] private bool drawAttackCheck = false;
    [SerializeField] private float debugDrawDuration = 0.5f;

    private float nextShootTime;

    private int shootTriggerHash;
    private int isShootingBoolHash;
    private int keepShootingBoolHash;

    private Coroutine shootingBoolRoutine;
    private bool isShooting;

    public bool IsShooting => isShooting;
    public float AttackRange => attackRange;

    private void Reset()
    {
        Transform root = transform.root;

        attackOrigin = transform;
        enemyAnimator = root.GetComponentInChildren<Animator>();
        enemyAnimatorDriver = root.GetComponent<EnemyAnimatorParameterDriver>();
        enemyWeaponSettings = GetComponent<EnemyWeaponSettings>();

        if (enemyWeaponSettings == null)
            enemyWeaponSettings = GetComponentInChildren<EnemyWeaponSettings>(true);

        if (weaponEffects == null)
            weaponEffects = GetComponentInChildren<WeaponEffects>(true);
    }

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

        if (attackOrigin == null)
            attackOrigin = transform;

        if (enemyAnimator == null)
            enemyAnimator = root.GetComponentInChildren<Animator>();

        if (enemyAnimatorDriver == null)
            enemyAnimatorDriver = root.GetComponent<EnemyAnimatorParameterDriver>();

        if (enemyWeaponSettings == null)
            enemyWeaponSettings = GetComponent<EnemyWeaponSettings>();

        if (enemyWeaponSettings == null)
            enemyWeaponSettings = GetComponentInChildren<EnemyWeaponSettings>(true);

        if (weaponEffects == null)
            weaponEffects = GetComponentInChildren<WeaponEffects>(true);
    }

    public bool CanShoot()
    {
        if (externalShootLock)
            return false;

        if (isShooting)
            return false;

        if (shootCooldown > 0f && Time.time < nextShootTime)
            return false;

        return true;
    }

    public bool NeedsReload()
    {
        return false;
    }
    public bool IsReloading()
    {
        return false;
    }

    public EnemyWeaponSettings GetEnemyWeaponSettings()
    {
        return enemyWeaponSettings;
    }

    // Same style / same port as EnemyWeaponShooter.
    public bool ShootAt(Vector3 targetPoint)
    {
        if (!CanShoot())
        {
            StopContinuousShootingState();
            return false;
        }

        if (faceTargetBeforeAttack)
            FacePoint(targetPoint);

        if (!IsPointInAttackRange(targetPoint))
        {
            if (logShot)
            {
                Debug.Log(
                    $"[EnemyMeleeAttacker] Target outside melee range. Target={targetPoint}",
                    this
                );
            }

            return false;
        }

        nextShootTime = Time.time + Mathf.Max(0f, shootCooldown);

        PlayShootAnimator();

        if (playWeaponEffectsOnAttack && weaponEffects != null)
            weaponEffects.PlayGunshot();

        if (logShot)
        {
            Debug.Log(
                $"[EnemyMeleeAttacker] ShootAt as melee attack. Target={targetPoint}",
                this
            );
        }

        return true;
    }

    public void ForceClearRuntimeState()
    {
        externalShootLock = false;

        if (shootingBoolRoutine != null)
        {
            StopCoroutine(shootingBoolRoutine);
            shootingBoolRoutine = null;
        }

        StopContinuousShootingState();
    }

    public bool CanAttack()
    {
        return CanShoot();
    }

    public bool TryAttackPoint(Vector3 targetPoint)
    {
        return ShootAt(targetPoint);
    }

    public bool TryAttackTarget(Transform target)
    {
        if (target == null)
            return false;

        return ShootAt(target.position);
    }

    public bool IsTargetInAttackRange(Transform target)
    {
        if (target == null)
            return false;

        return IsPointInAttackRange(target.position);
    }

    public bool IsPointInAttackRange(Vector3 targetPoint)
    {
        Vector3 origin = attackOrigin != null ? attackOrigin.position : transform.position;
        Vector3 toTarget = targetPoint - origin;
        toTarget.y = 0f;

        float distance = toTarget.magnitude;

        if (distance > attackRange)
            return false;

        if (toTarget.sqrMagnitude <= 0.0001f)
            return true;

        Vector3 forward = transform.root.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude <= 0.0001f)
            forward = transform.forward;

        forward.Normalize();
        toTarget.Normalize();

        float angle = Vector3.Angle(forward, toTarget);

        if (drawAttackCheck)
        {
            Debug.DrawRay(origin, forward * attackRange, Color.green, debugDrawDuration);
            Debug.DrawRay(origin, toTarget * attackRange, Color.red, debugDrawDuration);
        }

        return angle <= attackAngle * 0.5f;
    }

    private void PlayShootAnimator()
    {
        isShooting = true;

        if (useEnemyAnimatorDriver && enemyAnimatorDriver != null)
        {
            enemyAnimatorDriver.SetShooting(true);
            enemyAnimatorDriver.SetKeepShooting(useKeepShootingBool);
            enemyAnimatorDriver.TriggerShoot();
        }
        else if (enemyAnimator != null)
        {
            if (resetTriggerBeforeSet)
                enemyAnimator.ResetTrigger(shootTriggerHash);

            enemyAnimator.SetTrigger(shootTriggerHash);
            enemyAnimator.SetBool(isShootingBoolHash, true);

            if (!string.IsNullOrWhiteSpace(keepShootingBoolName))
                enemyAnimator.SetBool(keepShootingBoolHash, useKeepShootingBool);
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
        isShooting = false;

        if (useEnemyAnimatorDriver && enemyAnimatorDriver != null)
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

    private void FacePoint(Vector3 point)
    {
        Transform root = transform.root;

        Vector3 dir = point - root.position;
        dir.y = 0f;

        if (dir.sqrMagnitude <= 0.0001f)
            return;

        dir.Normalize();

        Quaternion targetRotation = Quaternion.LookRotation(dir, Vector3.up);

        if (snapFaceTarget)
        {
            root.rotation = targetRotation;
        }
        else
        {
            root.rotation = Quaternion.RotateTowards(
                root.rotation,
                targetRotation,
                Mathf.Max(0f, faceTurnSpeedDegrees) * Time.deltaTime
            );
        }
    }
}