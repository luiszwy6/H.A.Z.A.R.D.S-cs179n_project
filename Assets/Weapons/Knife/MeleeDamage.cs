using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class MeleeDamage : MonoBehaviour
{
    [Header("Damage Hitbox")]
    [SerializeField] private Collider damageHitbox;
    [SerializeField] private bool disableHitboxColliderOnAwake = true;
    [SerializeField] private bool enableHitboxColliderDuringDamageWindow = true;

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

    [Header("Damage Payload")]
    [SerializeField] private float Base_dmg = 25f;

    [Range(0, 2)]
    [SerializeField] private int ArmoPierLevel = 0;

    [SerializeField] private float knockbackValue = 0f;

    [Header("Hit Rules")]
    [SerializeField] private bool hitEachEnemyOncePerWindow = true;
    [SerializeField] private bool playHitPartShake = true;
    [SerializeField] private bool searchInParents = true;

    [Header("Damage Window")]
    [SerializeField] private bool damageWindowActiveOnEnable = false;
    [SerializeField] private float defaultDamageWindowDuration = 0.18f;

    [Header("Debug")]
    [SerializeField] private bool logHit = false;
    [SerializeField] private bool drawDebug = false;
    [SerializeField] private Color debugColor = Color.red;
    [SerializeField] private float debugDuration = 0.1f;

    private readonly HashSet<EnemyHealth> hitEnemiesThisWindow = new HashSet<EnemyHealth>();
    private Coroutine damageWindowRoutine;
    private bool damageWindowActive;

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

        if (disableHitboxColliderOnAwake && damageHitbox != null)
            damageHitbox.enabled = false;
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

    public void OpenDamageWindow()
    {
        damageWindowActive = true;
        hitEnemiesThisWindow.Clear();

        if (enableHitboxColliderDuringDamageWindow && damageHitbox != null)
            damageHitbox.enabled = true;
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

        if (enableHitboxColliderDuringDamageWindow && damageHitbox != null)
            damageHitbox.enabled = false;
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
        if (damageHitbox == null)
            return;

        Collider[] hits = QueryHitboxOverlap();

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

            EnemyHealth enemyHealth = GetEnemyHealthFromCollider(hitCollider);

            if (enemyHealth == null)
                continue;

            if (hitEachEnemyOncePerWindow && hitEnemiesThisWindow.Contains(enemyHealth))
                continue;

            if (!TryBuildRaycastHit(hitCollider, out RaycastHit hit))
                continue;

            bool enemyHandled = enemyHealth.TryApplyBulletDamage(
                hit,
                Mathf.Max(0f, Base_dmg),
                Mathf.Clamp(ArmoPierLevel, 0, 2),
                Mathf.Max(0f, knockbackValue),
                out float appliedDamage,
                out bool triggeredKnockback
            );

            if (!enemyHandled)
                continue;

            hitEnemiesThisWindow.Add(enemyHealth);

            if (playHitPartShake)
                TryPlayHitPartShake(hitCollider);

            if (logHit)
            {
                Debug.Log(
                    $"[MeleeDamage] Hit enemy={enemyHealth.name}, Hitbox={hitCollider.name}, Base_dmg={Base_dmg}, ArmoPierLevel={ArmoPierLevel}, Knockback={knockbackValue}, Applied={appliedDamage}, KnockbackTriggered={triggeredKnockback}",
                    this
                );
            }

            if (drawDebug)
                Debug.DrawLine(GetHitboxCenter(), hit.point, debugColor, debugDuration, false);
        }
    }

    private Collider[] QueryHitboxOverlap()
    {
        if (damageHitbox == null)
            return null;

        BoxCollider box = damageHitbox as BoxCollider;

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

        SphereCollider sphere = damageHitbox as SphereCollider;

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

        CapsuleCollider capsule = damageHitbox as CapsuleCollider;

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

        Bounds bounds = damageHitbox.bounds;

        return Physics.OverlapBox(
            bounds.center,
            bounds.extents,
            Quaternion.identity,
            targetMask,
            triggerInteraction
        );
    }

    private bool TryBuildRaycastHit(Collider targetCollider, out RaycastHit hit)
    {
        hit = default;

        if (targetCollider == null)
            return false;

        Vector3 source = GetHitboxCenter();
        Vector3 target = targetCollider.bounds.center;
        Vector3 dir = target - source;

        if (dir.sqrMagnitude <= 0.0001f)
            dir = transform.forward;

        if (dir.sqrMagnitude <= 0.0001f)
            dir = Vector3.forward;

        dir.Normalize();

        float hitboxRadius = GetApproxHitboxRadius();
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

    private Vector3 GetHitboxCenter()
    {
        if (damageHitbox == null)
            return transform.position;

        BoxCollider box = damageHitbox as BoxCollider;

        if (box != null)
            return box.transform.TransformPoint(box.center);

        SphereCollider sphere = damageHitbox as SphereCollider;

        if (sphere != null)
            return sphere.transform.TransformPoint(sphere.center);

        CapsuleCollider capsule = damageHitbox as CapsuleCollider;

        if (capsule != null)
            return capsule.transform.TransformPoint(capsule.center);

        return damageHitbox.bounds.center;
    }

    private float GetApproxHitboxRadius()
    {
        if (damageHitbox == null)
            return 0.1f;

        return Mathf.Max(0.01f, damageHitbox.bounds.extents.magnitude);
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