using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class MeleeDamage : MonoBehaviour
{
    public enum MeleeDamageMode
    {
        Normal,
        BackStab
    }

    [Header("Damage Mode")]
    [SerializeField] private MeleeDamageMode currentDamageMode = MeleeDamageMode.Normal;
    [SerializeField] private bool resetModeToNormalOnClose = true;

    [Header("Normal Damage Hitbox")]
    [SerializeField] private Collider damageHitbox;
    [SerializeField] private bool disableHitboxColliderOnAwake = true;
    [SerializeField] private bool enableHitboxColliderDuringDamageWindow = true;

    [Header("BackStab Damage Hitbox")]
    [SerializeField] private Collider backStabDamageHitbox;
    [SerializeField] private bool useSeparateBackStabHitbox = true;

    [Header("Find Child Hitbox")]
    [SerializeField] private bool includeInactiveColliders = true;
    [SerializeField] private bool triggerCollidersOnly = true;
    [SerializeField] private bool excludeThisObjectColliders = false;
    [SerializeField] private LayerMask findHitboxLayers = ~0;

    [Header("Target Query")]
    [SerializeField] private LayerMask targetMask = ~0;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;
    [SerializeField] private Transform ownerRoot;
    [SerializeField] private bool ignoreOwnerRoot = true;

    [Header("Normal Damage Payload")]
    [SerializeField] private float Base_dmg = 25f;

    [Range(0, 2)]
    [SerializeField] private int ArmoPierLevel = 0;

    [SerializeField] private float knockbackValue = 0f;

    [Header("BackStab Damage Payload")]
    [SerializeField] private float backStabBase_dmg = 60f;

    [Range(0, 2)]
    [SerializeField] private int backStabArmoPierLevel = 0;

    [SerializeField] private float backStabKnockbackValue = 0f;

    [Header("Runtime Damage")]
    [SerializeField] private float runtimeDamageMultiplier = 1f;
    [SerializeField] private bool resetRuntimeDamageMultiplierOnClose = true;

    [Header("Hit Rules")]
    [SerializeField] private bool hitEachEnemyOncePerWindow = true;
    [SerializeField] private bool playHitPartShake = true;
    [SerializeField] private bool playEnemyHitAudio = true;
    [SerializeField] private bool searchInParents = true;

    [Header("Blood Effect")]
    [SerializeField] private bool playMeleeBlood = true;
    [SerializeField] private bool requireAppliedDamageForBlood = true;
    [SerializeField] private GameObject normalMeleeBloodPrefab;
    [SerializeField] private GameObject backStabBloodPrefab;
    [SerializeField] private bool alignBloodToHitNormal = true;
    [SerializeField] private Vector3 bloodRotationOffsetEuler;
    [SerializeField] private float normalMeleeBloodScale = 1f;
    [SerializeField] private float backStabBloodScale = 1f;
    [SerializeField] private bool parentBloodToHitCollider = false;
    [SerializeField] private bool autoDestroySpawnedBlood = false;
    [SerializeField] private float bloodDestroyDelay = 3f;

    [Header("Damage Window")]
    [SerializeField] private bool damageWindowActiveOnEnable = false;
    [SerializeField] private float defaultDamageWindowDuration = 0.18f;

    [Header("Debug")]
    [SerializeField] private bool logHit = false;
    [SerializeField] private bool logBlood = false;
    [SerializeField] private bool drawDebug = false;
    [SerializeField] private Color debugColor = Color.red;
    [SerializeField] private float debugDuration = 0.1f;

    private readonly HashSet<EnemyHealth> hitEnemiesThisWindow = new HashSet<EnemyHealth>();
    private Coroutine damageWindowRoutine;
    private bool damageWindowActive;

    public MeleeDamageMode CurrentDamageMode => currentDamageMode;
    public bool IsBackStabMode => currentDamageMode == MeleeDamageMode.BackStab;
    public float RuntimeDamageMultiplier => Mathf.Max(0f, runtimeDamageMultiplier);

    private void Reset()
    {
        ownerRoot = transform.root;
        FindChildDamageHitbox();
    }

    private void Awake()
    {
        if (ownerRoot == null)
            ownerRoot = transform.root;

        if (damageHitbox == null)
            FindChildDamageHitbox();

        if (disableHitboxColliderOnAwake)
            DisableAllDamageHitboxes();
    }

    private void OnEnable()
    {
        if (damageWindowActiveOnEnable)
            OpenDamageWindow();
    }

    private void OnDisable()
    {
        CloseDamageWindow();
    }

    private void Update()
    {
        if (!damageWindowActive)
            return;

        ProcessDamageHitbox();
    }

    [ContextMenu("Find Child Damage Hitbox")]
    public void FindChildDamageHitbox()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(includeInactiveColliders);

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];

            if (!ShouldUseAsDamageHitbox(col))
                continue;

            damageHitbox = col;
            return;
        }
    }

    public void SetDamageMode(MeleeDamageMode mode)
    {
        currentDamageMode = mode;

        if (damageWindowActive)
            ApplyActiveHitboxColliderState();
    }

    public void UseNormalDamage()
    {
        SetDamageMode(MeleeDamageMode.Normal);
    }

    public void UseBackStabDamage()
    {
        SetDamageMode(MeleeDamageMode.BackStab);
    }

    public void SetRuntimeDamageMultiplier(float multiplier)
    {
        runtimeDamageMultiplier = Mathf.Max(0f, multiplier);
    }

    public void ResetRuntimeDamageMultiplier()
    {
        runtimeDamageMultiplier = 1f;
    }

    public void OpenDamageWindow()
    {
        damageWindowActive = true;
        hitEnemiesThisWindow.Clear();

        ApplyActiveHitboxColliderState();
    }

    public void CloseDamageWindow()
    {
        damageWindowActive = false;
        hitEnemiesThisWindow.Clear();

        if (damageWindowRoutine != null)
        {
            StopCoroutine(damageWindowRoutine);
            damageWindowRoutine = null;
        }

        DisableAllDamageHitboxes();

        if (resetRuntimeDamageMultiplierOnClose)
            ResetRuntimeDamageMultiplier();

        if (resetModeToNormalOnClose)
            currentDamageMode = MeleeDamageMode.Normal;
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

    private IEnumerator DamageWindowRoutine(float seconds)
    {
        OpenDamageWindow();

        yield return new WaitForSeconds(Mathf.Max(0.01f, seconds));

        CloseDamageWindow();
        damageWindowRoutine = null;
    }

    private void ProcessDamageHitbox()
    {
        Collider activeHitbox = GetActiveDamageHitbox();

        if (activeHitbox == null)
            return;

        Collider[] hits = QueryHitboxOverlap(activeHitbox);

        if (hits == null || hits.Length == 0)
            return;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i];

            if (hitCollider == null)
                continue;

            if (hitCollider == activeHitbox)
                continue;

            if (ShouldIgnoreCollider(hitCollider))
                continue;

            EnemyHealth enemyHealth = GetEnemyHealthFromCollider(hitCollider);

            if (enemyHealth == null)
                continue;

            if (hitEachEnemyOncePerWindow && hitEnemiesThisWindow.Contains(enemyHealth))
                continue;

            if (!TryBuildRaycastHit(activeHitbox, hitCollider, out RaycastHit hit))
                continue;

            float finalBaseDamage = GetActiveBaseDamage() * RuntimeDamageMultiplier;

            bool enemyHandled = enemyHealth.TryApplyBulletDamage(
                hit,
                finalBaseDamage,
                GetActiveArmorPierceLevel(),
                GetActiveKnockbackValue(),
                out float appliedDamage,
                out bool triggeredKnockback
            );

            if (!enemyHandled)
                continue;

            bool fatal = enemyHealth.IsDead;

            hitEnemiesThisWindow.Add(enemyHealth);

            if (playHitPartShake)
                TryPlayHitPartShake(hitCollider);

            if (playMeleeBlood)
                TrySpawnMeleeBlood(hitCollider, hit.point, hit.normal, appliedDamage);

            if (playEnemyHitAudio)
                TryPlayEnemyMeleeHitAudio(hitCollider, hit.point, appliedDamage, fatal);

            if (logHit)
            {
                Debug.Log(
                    $"[MeleeDamage] Mode={currentDamageMode}, Hit enemy={enemyHealth.name}, Hitbox={hitCollider.name}, BaseDamage={GetActiveBaseDamage()}, RuntimeMultiplier={RuntimeDamageMultiplier}, FinalBaseDamage={finalBaseDamage}, ArmoPierLevel={GetActiveArmorPierceLevel()}, Knockback={GetActiveKnockbackValue()}, Applied={appliedDamage}, KnockbackTriggered={triggeredKnockback}, Fatal={fatal}",
                    this
                );
            }

            if (drawDebug)
                Debug.DrawLine(GetHitboxCenter(activeHitbox), hit.point, debugColor, debugDuration, false);
        }
    }

    private Collider GetActiveDamageHitbox()
    {
        if (currentDamageMode == MeleeDamageMode.BackStab &&
            useSeparateBackStabHitbox &&
            backStabDamageHitbox != null)
        {
            return backStabDamageHitbox;
        }

        return damageHitbox;
    }

    private float GetActiveBaseDamage()
    {
        if (currentDamageMode == MeleeDamageMode.BackStab)
            return Mathf.Max(0f, backStabBase_dmg);

        return Mathf.Max(0f, Base_dmg);
    }

    private int GetActiveArmorPierceLevel()
    {
        if (currentDamageMode == MeleeDamageMode.BackStab)
            return Mathf.Clamp(backStabArmoPierLevel, 0, 2);

        return Mathf.Clamp(ArmoPierLevel, 0, 2);
    }

    private float GetActiveKnockbackValue()
    {
        if (currentDamageMode == MeleeDamageMode.BackStab)
            return Mathf.Max(0f, backStabKnockbackValue);

        return Mathf.Max(0f, knockbackValue);
    }

    private GameObject GetActiveBloodPrefab()
    {
        if (currentDamageMode == MeleeDamageMode.BackStab && backStabBloodPrefab != null)
            return backStabBloodPrefab;

        return normalMeleeBloodPrefab;
    }

    private float GetActiveBloodScale()
    {
        if (currentDamageMode == MeleeDamageMode.BackStab)
            return Mathf.Max(0.001f, backStabBloodScale);

        return Mathf.Max(0.001f, normalMeleeBloodScale);
    }

    private void TrySpawnMeleeBlood(
        Collider hitCollider,
        Vector3 hitPoint,
        Vector3 hitNormal,
        float appliedDamage)
    {
        if (hitCollider == null)
            return;

        if (requireAppliedDamageForBlood && appliedDamage <= 0f)
            return;

        GameObject prefab = GetActiveBloodPrefab();

        if (prefab == null)
            return;

        Quaternion rotation = ResolveBloodRotation(hitNormal);
        Transform parent = parentBloodToHitCollider ? hitCollider.transform : null;

        GameObject instance = Instantiate(prefab, hitPoint, rotation, parent);
        instance.transform.localScale = Vector3.one * GetActiveBloodScale();

        if (autoDestroySpawnedBlood)
            Destroy(instance, Mathf.Max(0.01f, bloodDestroyDelay));

        if (logBlood)
        {
            Debug.Log(
                $"[MeleeDamage] Blood spawned. Mode={currentDamageMode}, Prefab={prefab.name}, Hitbox={hitCollider.name}, AppliedDamage={appliedDamage}",
                this
            );
        }
    }

    private Quaternion ResolveBloodRotation(Vector3 hitNormal)
    {
        Quaternion baseRotation = transform.rotation;

        if (alignBloodToHitNormal && hitNormal.sqrMagnitude > 0.0001f)
        {
            Vector3 forward = hitNormal.normalized;
            Vector3 up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.98f
                ? Vector3.right
                : Vector3.up;

            baseRotation = Quaternion.LookRotation(forward, up);
        }

        return baseRotation * Quaternion.Euler(bloodRotationOffsetEuler);
    }

    private void ApplyActiveHitboxColliderState()
    {
        if (!enableHitboxColliderDuringDamageWindow)
            return;

        Collider activeHitbox = GetActiveDamageHitbox();

        if (damageHitbox != null)
            damageHitbox.enabled = activeHitbox == damageHitbox;

        if (backStabDamageHitbox != null && backStabDamageHitbox != damageHitbox)
            backStabDamageHitbox.enabled = activeHitbox == backStabDamageHitbox;
    }

    private void DisableAllDamageHitboxes()
    {
        if (damageHitbox != null)
            damageHitbox.enabled = false;

        if (backStabDamageHitbox != null && backStabDamageHitbox != damageHitbox)
            backStabDamageHitbox.enabled = false;
    }

    private Collider[] QueryHitboxOverlap(Collider activeHitbox)
    {
        if (activeHitbox == null)
            return null;

        BoxCollider box = activeHitbox as BoxCollider;

        if (box != null)
        {
            Vector3 center = box.transform.TransformPoint(box.center);
            Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, AbsVector3(box.transform.lossyScale));
            Quaternion rotation = box.transform.rotation;

            return Physics.OverlapBox(
                center,
                halfExtents,
                rotation,
                targetMask,
                triggerInteraction
            );
        }

        SphereCollider sphere = activeHitbox as SphereCollider;

        if (sphere != null)
        {
            Vector3 center = sphere.transform.TransformPoint(sphere.center);
            float scale = MaxAbsComponent(sphere.transform.lossyScale);
            float radius = Mathf.Max(0.001f, sphere.radius * scale);

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

    private bool TryBuildRaycastHit(Collider activeHitbox, Collider targetCollider, out RaycastHit hit)
    {
        hit = default;

        if (activeHitbox == null || targetCollider == null)
            return false;

        Vector3 source = GetHitboxCenter(activeHitbox);
        Vector3 target = targetCollider.bounds.center;
        Vector3 dir = target - source;

        if (dir.sqrMagnitude <= 0.0001f)
            dir = transform.forward;

        if (dir.sqrMagnitude <= 0.0001f)
            dir = Vector3.forward;

        dir.Normalize();

        float hitboxRadius = GetApproxHitboxRadius(activeHitbox);
        float targetRadius = targetCollider.bounds.extents.magnitude;
        float distance = Vector3.Distance(source, target) + hitboxRadius + targetRadius + 0.5f;

        Vector3 origin = source - dir * Mathf.Max(0.05f, hitboxRadius + 0.05f);
        Ray ray = new Ray(origin, dir);

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

            EnemyHealth expectedEnemy = GetEnemyHealthFromCollider(targetCollider);
            EnemyHealth hitEnemy = GetEnemyHealthFromCollider(hit.collider);

            if (expectedEnemy != null && expectedEnemy == hitEnemy)
                return true;
        }

        return false;
    }

    private void TryPlayHitPartShake(Collider hitCollider)
    {
        if (hitCollider == null)
            return;

        HitPartShakeReaction shake = searchInParents
            ? hitCollider.GetComponentInParent<HitPartShakeReaction>()
            : hitCollider.GetComponent<HitPartShakeReaction>();

        if (shake == null)
            return;

        shake.PlayHurtbox(hitCollider);
    }

    private void TryPlayEnemyMeleeHitAudio(
        Collider hitCollider,
        Vector3 hitPoint,
        float appliedDamage,
        bool fatal)
    {
        if (hitCollider == null)
            return;

        EnemyAudioFeedback audioFeedback = searchInParents
            ? hitCollider.GetComponentInParent<EnemyAudioFeedback>()
            : hitCollider.GetComponent<EnemyAudioFeedback>();

        if (audioFeedback == null)
            return;

        audioFeedback.PlayMeleeHit(hitCollider, hitPoint, appliedDamage, currentDamageMode, fatal);
    }

    private EnemyHealth GetEnemyHealthFromCollider(Collider col)
    {
        if (col == null)
            return null;

        return searchInParents
            ? col.GetComponentInParent<EnemyHealth>()
            : col.GetComponent<EnemyHealth>();
    }

    private bool ShouldUseAsDamageHitbox(Collider col)
    {
        if (col == null)
            return false;

        if (excludeThisObjectColliders && col.transform == transform)
            return false;

        if (triggerCollidersOnly && !col.isTrigger)
            return false;

        if (!IsLayerIncluded(findHitboxLayers, col.gameObject.layer))
            return false;

        return true;
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

    private bool IsLayerIncluded(LayerMask mask, int layer)
    {
        return (mask.value & (1 << layer)) != 0;
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
}