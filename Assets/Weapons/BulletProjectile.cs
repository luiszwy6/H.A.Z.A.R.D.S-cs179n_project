using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BulletProjectile : MonoBehaviour
{
    [Header("Projectile")]
    public float speed = 120f;
    public float maxDistance = 50f;
    public float radius = 0.04f;

    [Header("Hit Query")]
    public LayerMask hitMask = ~0;
    public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

    [Header("Damage Payload")]
    [SerializeField] private float Base_dmg = 0f;

    [Range(0, 2)]
    [SerializeField] private int ArmoPierLevel = 0;

    [SerializeField] private float knockbackValue = 0f;

    [Min(0)]
    [SerializeField] private int PenetrationLevel = 0;

    [Range(0f, 100f)]
    [SerializeField] private float penetrationDamageDecayPercent = 35f;

    [Header("Part Overrides")]
    [SerializeField] private WeaponDamagePartOverride[] partOverrides;

    [Header("Hit Reaction")]
    [SerializeField] private bool applyEnemyHealthDamage = true;
    [SerializeField] private bool playHitPartShake = true;
    [SerializeField] private bool searchInParents = true;

    [Header("Stun Reaction")]
    [SerializeField] private bool applyEnemyStun = true;
    [SerializeField] private bool applyStunBeforeDamage = false;
    [SerializeField] private bool logStunHit = false;

    [Header("Behavior")]
    public bool destroyOnAnyHit = true;
    [SerializeField] private float penetrationSkinWidth = 0.03f;
    [SerializeField] private int maxHitsPerFrame = 8;

    [Header("Debug")]
    public bool drawDebugLine = false;
    public Color debugColor = Color.red;
    [SerializeField] private bool logHit = false;
    [SerializeField] private bool logRawHit = false;
    [SerializeField] private bool drawHitDebug = false;
    [SerializeField] private Color hitDebugColor = Color.magenta;
    [SerializeField] private float hitDebugDuration = 0.35f;

    [Header("Gizmos / Debug View")]
    public bool drawGizmos = true;
    public bool drawOnlyWhenSelected = false;
    public bool drawSweepSegment = true;
    public Color gizmoSphereColor = Color.red;
    public Color gizmoSweepColor = Color.yellow;

    private Vector3 _dir;
    private float _traveled = 0f;
    private bool _inited = false;

    private float runtimeBaseDamage;
    private float runtimeKnockbackValue;
    private int runtimeRemainingPenetrations;

    private Vector3 _prevPosForGizmo;
    private bool _hasPrevPosForGizmo = false;

    private readonly List<EnemyHealth> penetratedEnemies = new List<EnemyHealth>();

    public void SetDamagePayload(
        float Base_dmg,
        int ArmoPierLevel,
        float knockbackValue,
        int PenetrationLevel,
        float penetrationDamageDecayPercent)
    {
        SetDamagePayload(
            Base_dmg,
            ArmoPierLevel,
            knockbackValue,
            PenetrationLevel,
            penetrationDamageDecayPercent,
            null
        );
    }

    public void SetDamagePayload(
        float Base_dmg,
        int ArmoPierLevel,
        float knockbackValue,
        int PenetrationLevel,
        float penetrationDamageDecayPercent,
        WeaponDamagePartOverride[] partOverrides)
    {
        this.Base_dmg = Mathf.Max(0f, Base_dmg);
        this.ArmoPierLevel = Mathf.Clamp(ArmoPierLevel, 0, 2);
        this.knockbackValue = Mathf.Max(0f, knockbackValue);
        this.PenetrationLevel = Mathf.Max(0, PenetrationLevel);
        this.penetrationDamageDecayPercent = Mathf.Clamp(penetrationDamageDecayPercent, 0f, 100f);
        this.partOverrides = partOverrides;

        runtimeBaseDamage = this.Base_dmg;
        runtimeKnockbackValue = this.knockbackValue;
        runtimeRemainingPenetrations = this.PenetrationLevel;
    }

    public void Init(
        Vector3 origin,
        Vector3 direction,
        float speed,
        float maxDistance,
        float radius,
        LayerMask hitMask,
        QueryTriggerInteraction triggerInteraction)
    {
        transform.position = origin;

        _dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        this.speed = Mathf.Max(0.01f, speed);
        this.maxDistance = Mathf.Max(0.01f, maxDistance);
        this.radius = Mathf.Max(0.001f, radius);

        this.hitMask = hitMask;
        this.triggerInteraction = triggerInteraction;

        _traveled = 0f;
        _inited = true;

        runtimeBaseDamage = Mathf.Max(0f, Base_dmg);
        runtimeKnockbackValue = Mathf.Max(0f, knockbackValue);
        runtimeRemainingPenetrations = Mathf.Max(0, PenetrationLevel);
        penetratedEnemies.Clear();

        transform.rotation = Quaternion.LookRotation(_dir, Vector3.up);

        _prevPosForGizmo = transform.position;
        _hasPrevPosForGizmo = true;
    }

    private void Update()
    {
        if (!_inited)
            return;

        float step = speed * Time.deltaTime;

        if (step <= 0f)
            return;

        Vector3 start = transform.position;
        Vector3 current = start;
        float remainingStep = step;

        _prevPosForGizmo = start;
        _hasPrevPosForGizmo = true;

        int hitCount = 0;

        while (remainingStep > 0.0001f && hitCount < Mathf.Max(1, maxHitsPerFrame))
        {
            if (!TryGetClosestValidHit(current, remainingStep, out RaycastHit hit))
            {
                Vector3 end = current + _dir * remainingStep;
                transform.position = end;
                _traveled += remainingStep;
                break;
            }

            hitCount++;

            float hitDistance = Mathf.Max(0f, hit.distance);
            transform.position = hit.point;
            _traveled += hitDistance;

            bool shouldStop = HandleHit(hit);

            if (drawHitDebug)
                Debug.DrawRay(hit.point, _dir * 0.5f, hitDebugColor, hitDebugDuration, false);

            if (shouldStop)
            {
                Destroy(gameObject);
                return;
            }

            float skin = Mathf.Max(0.001f, penetrationSkinWidth);
            current = hit.point + _dir * skin;
            remainingStep -= hitDistance + skin;

            if (_traveled >= maxDistance)
            {
                Destroy(gameObject);
                return;
            }
        }

        if (drawDebugLine)
            Debug.DrawLine(start, transform.position, debugColor, 0f, false);

        if (_traveled >= maxDistance)
            Destroy(gameObject);
    }

    private bool TryGetClosestValidHit(Vector3 from, float distance, out RaycastHit closestHit)
    {
        closestHit = default;

        RaycastHit[] hits = Physics.SphereCastAll(
            from,
            radius,
            _dir,
            distance,
            hitMask,
            triggerInteraction
        );

        if (hits == null || hits.Length == 0)
            return false;

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];

            if (hit.collider == null)
                continue;

            EnemyHealth enemyHealth = GetEnemyHealthFromCollider(hit.collider);

            if (logRawHit)
            {
                Debug.Log(
                    $"[BulletProjectile] Raw hit={hit.collider.name}, layer={LayerMask.LayerToName(hit.collider.gameObject.layer)}, enemyHealth={enemyHealth}",
                    this
                );
            }

            if (enemyHealth != null && penetratedEnemies.Contains(enemyHealth))
                continue;

            closestHit = hit;
            return true;
        }

        return false;
    }

    private bool HandleHit(RaycastHit hit)
    {
        EnemyHealth enemyHealth = GetEnemyHealthFromCollider(hit.collider);

        if (enemyHealth == null)
        {
            if (applyEnemyStun)
                TryApplyEnemyStun(hit);

            return destroyOnAnyHit;
        }

        bool enemyHandled = false;

        if (applyEnemyStun && applyStunBeforeDamage)
            TryApplyEnemyStun(hit);

        if (applyEnemyHealthDamage)
        {
            enemyHandled = enemyHealth.TryApplyBulletDamage(
                hit,
                runtimeBaseDamage,
                ArmoPierLevel,
                runtimeKnockbackValue,
                partOverrides,
                out float appliedDamage,
                out bool triggeredKnockback
            );

            if (logHit)
            {
                Debug.Log(
                    $"[BulletProjectile] Hit enemy={enemyHealth.name}, Base_dmg={runtimeBaseDamage}, ArmoPierLevel={ArmoPierLevel}, Knockback={runtimeKnockbackValue}, Applied={appliedDamage}, KnockbackTriggered={triggeredKnockback}",
                    this
                );
            }
        }

        if (applyEnemyStun && !applyStunBeforeDamage)
            TryApplyEnemyStun(hit);

        if (playHitPartShake)
            TryPlayHitPartShake(hit);

        if (!enemyHandled)
            return destroyOnAnyHit;

        penetratedEnemies.Add(enemyHealth);

        if (runtimeRemainingPenetrations > 0)
        {
            runtimeRemainingPenetrations--;
            ApplyPenetrationDecay();
            return false;
        }

        return true;
    }

    private void ApplyPenetrationDecay()
    {
        float keepFactor = 1f - Mathf.Clamp01(penetrationDamageDecayPercent / 100f);

        runtimeBaseDamage *= keepFactor;
        runtimeKnockbackValue *= keepFactor;
    }

    private EnemyHealth GetEnemyHealthFromCollider(Collider col)
    {
        if (col == null)
            return null;

        return searchInParents
            ? col.GetComponentInParent<EnemyHealth>()
            : col.GetComponent<EnemyHealth>();
    }

    private EnemyStunReceiver GetEnemyStunReceiverFromCollider(Collider col)
    {
        if (col == null)
            return null;

        return searchInParents
            ? col.GetComponentInParent<EnemyStunReceiver>()
            : col.GetComponent<EnemyStunReceiver>();
    }

    private void TryApplyEnemyStun(RaycastHit hit)
    {
        if (hit.collider == null)
            return;

        EnemyStunReceiver stunReceiver = GetEnemyStunReceiverFromCollider(hit.collider);

        if (stunReceiver == null)
            return;

        bool stunned = stunReceiver.TryApplyStunFromHit(hit.collider);

        if (logStunHit)
        {
            Debug.Log(
                $"[BulletProjectile] Stun check. Collider={hit.collider.name}, Receiver={stunReceiver.name}, Applied={stunned}",
                this
            );
        }
    }

    private void TryPlayHitPartShake(RaycastHit hit)
    {
        if (hit.collider == null)
            return;

        HitPartShakeReaction shake = searchInParents
            ? hit.collider.GetComponentInParent<HitPartShakeReaction>()
            : hit.collider.GetComponent<HitPartShakeReaction>();

        if (shake == null)
            return;

        shake.PlayHurtbox(hit.collider);
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos || drawOnlyWhenSelected)
            return;

        DrawProjectileGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos || !drawOnlyWhenSelected)
            return;

        DrawProjectileGizmos();
    }

    private void DrawProjectileGizmos()
    {
        float r = Mathf.Max(0.001f, radius);

        Gizmos.color = gizmoSphereColor;
        Gizmos.DrawWireSphere(transform.position, r);

        if (!drawSweepSegment || !_hasPrevPosForGizmo)
            return;

        Gizmos.color = gizmoSweepColor;
        Gizmos.DrawLine(_prevPosForGizmo, transform.position);
        Gizmos.DrawWireSphere(_prevPosForGizmo, r);
        Gizmos.DrawWireSphere(transform.position, r);
    }
}