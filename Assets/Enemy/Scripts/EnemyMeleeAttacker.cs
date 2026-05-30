using System.Collections;
using System.Collections.Generic;
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
    [SerializeField] private MeleeWeaponEffects meleeWeaponEffects;
    [SerializeField] private Axe_DamageSettings axeDamageSettings;

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

    [Header("Melee Damage Window")]
    [SerializeField] private Collider damageHitbox;
    [SerializeField] private bool autoFindDamageHitbox = true;
    [SerializeField] private bool includeInactiveHitboxes = true;
    [SerializeField] private bool triggerHitboxesOnly = true;
    [SerializeField] private LayerMask findHitboxLayers = ~0;
    [SerializeField] private bool disableHitboxColliderOnAwake = true;
    [SerializeField] private bool enableHitboxColliderDuringDamageWindow = true;
    [SerializeField] private LayerMask targetMask = ~0;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;
    [SerializeField] private Transform ownerRoot;
    [SerializeField] private bool ignoreOwnerRoot = true;
    [SerializeField] private bool searchPlayerHealthInParents = true;
    [SerializeField] private bool hitPlayerOncePerWindow = true;
    [SerializeField] private bool damageWindowActiveOnEnable = false;
    [SerializeField] private float defaultDamageWindowDuration = 0.18f;

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
    private Coroutine damageWindowRoutine;
    private bool isShooting;
    private bool damageWindowActive;
    private readonly HashSet<PlayerHealth> hitPlayersThisWindow = new HashSet<PlayerHealth>();

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

        if (meleeWeaponEffects == null)
            meleeWeaponEffects = GetComponentInChildren<MeleeWeaponEffects>(true);

        if (axeDamageSettings == null)
            axeDamageSettings = GetComponentInChildren<Axe_DamageSettings>(true);

        if (ownerRoot == null)
            ownerRoot = root;

        if (autoFindDamageHitbox && damageHitbox == null)
            FindChildDamageHitbox();
    }

    private void Awake()
    {
        ResolveReferences();

        shootTriggerHash = Animator.StringToHash(shootTriggerName);
        isShootingBoolHash = Animator.StringToHash(isShootingBoolName);
        keepShootingBoolHash = Animator.StringToHash(keepShootingBoolName);

        if (disableHitboxColliderOnAwake)
            SetDamageHitboxEnabled(false);
    }

    private void OnEnable()
    {
        if (damageWindowActiveOnEnable)
            OpenDamageWindow();
    }

    private void Update()
    {
        if (damageWindowActive)
            ProcessDamageHitbox();
    }

    private void OnDisable()
    {
        CloseDamageWindow();
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

        if (meleeWeaponEffects == null)
            meleeWeaponEffects = GetComponentInChildren<MeleeWeaponEffects>(true);

        if (axeDamageSettings == null)
            axeDamageSettings = GetComponentInChildren<Axe_DamageSettings>(true);

        if (ownerRoot == null)
            ownerRoot = root;

        if (autoFindDamageHitbox && damageHitbox == null)
            FindChildDamageHitbox();
    }

    [ContextMenu("Find Child Damage Hitbox")]
    public void FindChildDamageHitbox()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(includeInactiveHitboxes);

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];

            if (!ShouldUseAsDamageHitbox(col))
                continue;

            damageHitbox = col;
            return;
        }
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

        if (playWeaponEffectsOnAttack)
        {
            if (meleeWeaponEffects != null)
                meleeWeaponEffects.PlayMeleeEffect();
            else if (weaponEffects != null)
                weaponEffects.PlayGunshot();
        }

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

    public void OpenDamageWindow()
    {
        if (autoFindDamageHitbox && damageHitbox == null)
            FindChildDamageHitbox();

        damageWindowActive = true;
        hitPlayersThisWindow.Clear();
        SetDamageHitboxEnabled(true);
    }

    public void CloseDamageWindow()
    {
        damageWindowActive = false;
        hitPlayersThisWindow.Clear();

        if (damageWindowRoutine != null)
        {
            StopCoroutine(damageWindowRoutine);
            damageWindowRoutine = null;
        }

        SetDamageHitboxEnabled(false);
    }

    public void OpenDamageWindowForDefaultDuration()
    {
        OpenDamageWindowForSeconds(defaultDamageWindowDuration);
    }

    public void OpenDamageWindowForSeconds(float seconds)
    {
        if (damageWindowRoutine != null)
        {
            StopCoroutine(damageWindowRoutine);
            damageWindowRoutine = null;
        }

        damageWindowRoutine = StartCoroutine(DamageWindowRoutine(seconds));
    }

    public void EnableDamage()
    {
        OpenDamageWindow();
    }

    public void DisableDamage()
    {
        CloseDamageWindow();
    }

    public void EnableAxeHitbox()
    {
        OpenDamageWindow();
    }

    public void DisableAxeHitbox()
    {
        CloseDamageWindow();
    }

    private IEnumerator DamageWindowRoutine(float seconds)
    {
        OpenDamageWindow();

        yield return new WaitForSeconds(Mathf.Max(0.01f, seconds));

        CloseDamageWindow();
        damageWindowRoutine = null;
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

    private void ProcessDamageHitbox()
    {
        if (damageHitbox == null)
            return;

        Collider[] hits = QueryHitboxOverlap(damageHitbox);

        if (hits == null || hits.Length == 0)
            return;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i];

            if (hitCollider == null)
                continue;

            if (hitCollider == damageHitbox)
                continue;

            if (ShouldIgnoreCollider(hitCollider))
                continue;

            PlayerHealth playerHealth = GetPlayerHealthFromCollider(hitCollider);

            if (playerHealth == null)
                continue;

            if (hitPlayerOncePerWindow && hitPlayersThisWindow.Contains(playerHealth))
                continue;

            float damage = ResolveAxeDamage();

            if (damage <= 0f)
                continue;

            if (!TryBuildRaycastHit(damageHitbox, hitCollider, out RaycastHit hit))
                continue;

            bool handled = playerHealth.TryApplyBulletDamage(
                hit,
                damage,
                ResolveAxeArmorPierceLevel(),
                0f,
                out float appliedDamage,
                out bool triggeredKnockback
            );

            if (!handled)
                continue;

            hitPlayersThisWindow.Add(playerHealth);

            if (logShot || ShouldLogAxeDamagePayload())
            {
                Debug.Log(
                    $"[EnemyMeleeAttacker] Axe hit player={playerHealth.name}, BaseDamage={damage}, ArmorPierce={ResolveAxeArmorPierceLevel()}, Applied={appliedDamage}, KnockbackTriggered={triggeredKnockback}",
                    this
                );
            }
        }
    }

    private float ResolveAxeDamage()
    {
        if (axeDamageSettings == null)
            axeDamageSettings = GetComponentInChildren<Axe_DamageSettings>(true);

        if (axeDamageSettings == null)
            return 0f;

        return axeDamageSettings.BaseDamage;
    }

    private int ResolveAxeArmorPierceLevel()
    {
        if (axeDamageSettings == null)
            axeDamageSettings = GetComponentInChildren<Axe_DamageSettings>(true);

        if (axeDamageSettings == null)
            return 0;

        return axeDamageSettings.ArmorPierceLevel;
    }

    private bool ShouldLogAxeDamagePayload()
    {
        if (axeDamageSettings == null)
            return false;

        return axeDamageSettings.LogDamagePayload;
    }

    private Collider[] QueryHitboxOverlap(Collider activeHitbox)
    {
        BoxCollider box = activeHitbox as BoxCollider;

        if (box != null)
        {
            Vector3 center = box.transform.TransformPoint(box.center);
            Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, AbsVector3(box.transform.lossyScale));

            return Physics.OverlapBox(
                center,
                halfExtents,
                box.transform.rotation,
                targetMask,
                triggerInteraction
            );
        }

        SphereCollider sphere = activeHitbox as SphereCollider;

        if (sphere != null)
        {
            Vector3 center = sphere.transform.TransformPoint(sphere.center);
            float radius = Mathf.Max(0.001f, sphere.radius * MaxAbsComponent(sphere.transform.lossyScale));

            return Physics.OverlapSphere(
                center,
                radius,
                targetMask,
                triggerInteraction
            );
        }

        CapsuleCollider capsule = activeHitbox as CapsuleCollider;

        if (capsule != null)
        {
            GetCapsuleWorldPoints(
                capsule,
                out Vector3 pointA,
                out Vector3 pointB,
                out float radius
            );

            return Physics.OverlapCapsule(
                pointA,
                pointB,
                radius,
                targetMask,
                triggerInteraction
            );
        }

        Bounds bounds = activeHitbox.bounds;

        return Physics.OverlapBox(
            bounds.center,
            bounds.extents,
            Quaternion.identity,
            targetMask,
            triggerInteraction
        );
    }

    private void SetDamageHitboxEnabled(bool enabled)
    {
        if (!enableHitboxColliderDuringDamageWindow)
            return;

        if (damageHitbox != null)
            damageHitbox.enabled = enabled;
    }

    private PlayerHealth GetPlayerHealthFromCollider(Collider col)
    {
        if (col == null)
            return null;

        return searchPlayerHealthInParents
            ? col.GetComponentInParent<PlayerHealth>()
            : col.GetComponent<PlayerHealth>();
    }

    private bool ShouldIgnoreCollider(Collider col)
    {
        if (col == null)
            return true;

        if (!ignoreOwnerRoot)
            return false;

        if (ownerRoot == null)
            return false;

        return col.transform == ownerRoot || col.transform.IsChildOf(ownerRoot);
    }

    private bool ShouldUseAsDamageHitbox(Collider col)
    {
        if (col == null)
            return false;

        if (col.transform == transform || col.transform == transform.root)
            return false;

        if (triggerHitboxesOnly && !col.isTrigger)
            return false;

        return IsLayerIncluded(findHitboxLayers, col.gameObject.layer);
    }

    private bool IsLayerIncluded(LayerMask mask, int layer)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    private bool TryBuildRaycastHit(Collider activeHitbox, Collider targetCollider, out RaycastHit hit)
    {
        hit = default;

        if (activeHitbox == null || targetCollider == null)
            return false;

        Vector3 source = GetHitboxCenter(activeHitbox);
        Vector3 target = targetCollider.bounds.center;
        Vector3 direction = target - source;

        if (direction.sqrMagnitude <= 0.0001f)
            direction = transform.root.forward;

        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector3.forward;

        direction.Normalize();

        float hitboxRadius = GetApproxHitboxRadius(activeHitbox);
        float targetRadius = targetCollider.bounds.extents.magnitude;
        float distance = Vector3.Distance(source, target) + hitboxRadius + targetRadius + 0.5f;
        Vector3 origin = source - direction * Mathf.Max(0.05f, hitboxRadius + 0.05f);
        Ray ray = new Ray(origin, direction);

        if (targetCollider.Raycast(ray, out hit, distance))
            return true;

        if (Physics.Raycast(
            ray,
            out hit,
            distance,
            targetMask,
            triggerInteraction))
        {
            if (hit.collider == targetCollider)
                return true;

            PlayerHealth expectedPlayer = GetPlayerHealthFromCollider(targetCollider);
            PlayerHealth hitPlayer = GetPlayerHealthFromCollider(hit.collider);

            if (expectedPlayer != null && expectedPlayer == hitPlayer)
                return true;
        }

        return false;
    }

    private Vector3 GetHitboxCenter(Collider activeHitbox)
    {
        if (activeHitbox == null)
            return transform.position;

        BoxCollider box = activeHitbox as BoxCollider;

        if (box != null)
            return box.transform.TransformPoint(box.center);

        SphereCollider sphere = activeHitbox as SphereCollider;

        if (sphere != null)
            return sphere.transform.TransformPoint(sphere.center);

        CapsuleCollider capsule = activeHitbox as CapsuleCollider;

        if (capsule != null)
            return capsule.transform.TransformPoint(capsule.center);

        return activeHitbox.bounds.center;
    }

    private float GetApproxHitboxRadius(Collider activeHitbox)
    {
        if (activeHitbox == null)
            return 0.1f;

        return Mathf.Max(0.01f, activeHitbox.bounds.extents.magnitude);
    }

    private void GetCapsuleWorldPoints(
        CapsuleCollider capsule,
        out Vector3 pointA,
        out Vector3 pointB,
        out float radius)
    {
        Transform t = capsule.transform;
        Vector3 center = t.TransformPoint(capsule.center);
        Vector3 scale = AbsVector3(t.lossyScale);

        Vector3 localAxis;
        float axisScale;
        float radiusScale;

        if (capsule.direction == 0)
        {
            localAxis = Vector3.right;
            axisScale = scale.x;
            radiusScale = Mathf.Max(scale.y, scale.z);
        }
        else if (capsule.direction == 1)
        {
            localAxis = Vector3.up;
            axisScale = scale.y;
            radiusScale = Mathf.Max(scale.x, scale.z);
        }
        else
        {
            localAxis = Vector3.forward;
            axisScale = scale.z;
            radiusScale = Mathf.Max(scale.x, scale.y);
        }

        radius = Mathf.Max(0.001f, capsule.radius * radiusScale);
        float height = Mathf.Max(radius * 2f, capsule.height * axisScale);
        float halfSegment = Mathf.Max(0f, (height * 0.5f) - radius);
        Vector3 worldAxis = t.TransformDirection(localAxis).normalized;

        pointA = center + worldAxis * halfSegment;
        pointB = center - worldAxis * halfSegment;
    }

    private Vector3 AbsVector3(Vector3 v)
    {
        return new Vector3(
            Mathf.Abs(v.x),
            Mathf.Abs(v.y),
            Mathf.Abs(v.z)
        );
    }

    private float MaxAbsComponent(Vector3 v)
    {
        v = AbsVector3(v);
        return Mathf.Max(v.x, Mathf.Max(v.y, v.z));
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
