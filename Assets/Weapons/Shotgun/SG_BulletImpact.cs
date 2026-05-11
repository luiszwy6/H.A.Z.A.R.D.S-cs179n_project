using System.Collections;
using UnityEngine;
using FIMSpace.FProceduralAnimation;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class BulletImpact : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Collider impactCollider;

    [Header("Target")]
    [SerializeField] private LayerMask ragdollHitMask = ~0;
    [SerializeField] private bool requireTargetLayer = false;
    [SerializeField] private bool searchInParents = true;
    [SerializeField] private bool alsoSearchThroughEnemyHealth = true;

    [Header("Owner Ignore")]
    [SerializeField] private Transform ownerRoot;
    [SerializeField] private bool ignoreOwnerColliders = true;
    [SerializeField] private bool ignoreOwnerOnInit = true;

    [Header("Ragdoll Animator 2")]
    [SerializeField] private bool enableRagdollComponentIfDisabled = true;
    [SerializeField] private float targetRagdollBlend = 1f;
    [SerializeField] private bool switchToFall = true;
    [SerializeField] private bool notifyGetUpController = true;

    [Header("Impact Direction")]
    [SerializeField] private bool useStoredVelocityDirection = true;
    [SerializeField] private float upwardImpact = 0.02f;

    [Header("Impact Power")]
    [SerializeField] private bool useVelocityMagnitudeForImpact = false;
    [SerializeField] private float fixedImpactPower = 45f;
    [SerializeField] private float velocityImpactMultiplier = 0.5f;
    [SerializeField] private float minimumImpactMagnitude = 15f;
    [SerializeField] private float maximumImpactMagnitude = 120f;

    [Header("Impact Application")]
    [SerializeField] private bool addFullImpact = true;
    [SerializeField] private bool addCoreImpact = false;
    [SerializeField] private int impactFixedUpdateRepeats = 1;
    [SerializeField] private AnimationCurve impactFalloff = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Header("Projectile")]
    [SerializeField] private bool setupRigidbodyOnInit = true;
    [SerializeField] private bool useGravity = false;
    [SerializeField] private CollisionDetectionMode collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    [SerializeField] private RigidbodyInterpolation interpolation = RigidbodyInterpolation.Interpolate;

    [Header("Hit Behavior")]
    [SerializeField] private bool destroyOnRagdollHit = true;
    [SerializeField] private bool destroyOnNonRagdollHit = true;
    [SerializeField] private bool disableColliderAfterHit = true;
    [SerializeField] private bool stopProjectileAfterHit = true;
    [SerializeField] private bool hideRendererAfterHit = true;

    [Header("Lifetime")]
    [SerializeField] private bool destroyAfterLifetime = true;
    [SerializeField] private float lifetime = 1.2f;
    [SerializeField] private bool destroyAfterMaxDistance = true;

    [Header("Debug")]
    [SerializeField] private bool logHit = false;
    [SerializeField] private bool drawImpactDebug = false;
    [SerializeField] private Color impactDebugColor = Color.red;
    [SerializeField] private float impactDebugDuration = 0.6f;

    private Vector3 fireDirection = Vector3.forward;
    private Vector3 lastVelocity;
    private Vector3 spawnPosition;
    private float maxDistance = 25f;
    private bool initialized;
    private bool consumedHit;
    private Coroutine impactRoutine;

    private void Reset()
    {
        rb = GetComponent<Rigidbody>();
        impactCollider = GetComponent<Collider>();

        if (impactCollider != null)
            impactCollider.isTrigger = false;
    }

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (impactCollider == null)
            impactCollider = GetComponent<Collider>();
    }

    public void Init(
        Vector3 origin,
        Vector3 direction,
        float speed,
        float maxDistance,
        LayerMask ragdollHitMask,
        Transform ownerRoot)
    {
        transform.position = origin;

        fireDirection = direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : transform.forward;

        if (fireDirection.sqrMagnitude <= 0.0001f)
            fireDirection = Vector3.forward;

        transform.rotation = Quaternion.LookRotation(fireDirection, Vector3.up);

        this.maxDistance = Mathf.Max(0.01f, maxDistance);
        this.ragdollHitMask = ragdollHitMask;
        this.ownerRoot = ownerRoot;

        spawnPosition = origin;
        initialized = true;
        consumedHit = false;

        if (impactCollider != null)
            impactCollider.enabled = true;

        if (rb != null)
        {
            if (setupRigidbodyOnInit)
            {
                rb.useGravity = useGravity;
                rb.isKinematic = false;
                rb.collisionDetectionMode = collisionDetectionMode;
                rb.interpolation = interpolation;
            }

            rb.linearVelocity = fireDirection * Mathf.Max(0.01f, speed);
            rb.angularVelocity = Vector3.zero;
            lastVelocity = rb.linearVelocity;
        }

        if (ignoreOwnerOnInit)
            IgnoreOwnerColliders();

        if (destroyAfterLifetime)
            Destroy(gameObject, Mathf.Max(0.01f, lifetime));
    }

    private void FixedUpdate()
    {
        if (rb == null)
            return;

        if (!consumedHit)
            lastVelocity = rb.linearVelocity;
    }

    private void Update()
    {
        if (!initialized)
            return;

        if (!destroyAfterMaxDistance)
            return;

        float traveled = Vector3.Distance(spawnPosition, transform.position);

        if (traveled >= maxDistance)
            Destroy(gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!initialized)
            return;

        if (collision == null)
            return;

        ContactPoint contact = collision.contactCount > 0
            ? collision.GetContact(0)
            : default;

        Vector3 point = collision.contactCount > 0
            ? contact.point
            : transform.position;

        HandleHit(collision.collider, point, collision.relativeVelocity);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!initialized)
            return;

        HandleHit(other, transform.position, Vector3.zero);
    }

    private void HandleHit(Collider hitCollider, Vector3 hitPoint, Vector3 relativeVelocity)
    {
        if (consumedHit)
            return;

        if (hitCollider == null)
            return;

        if (ShouldIgnoreCollider(hitCollider))
            return;

        bool validTargetLayer = IsLayerIncluded(ragdollHitMask, hitCollider.gameObject.layer);

        if (requireTargetLayer && !validTargetLayer)
        {
            ConsumeNonRagdollHit();
            return;
        }

        RagdollAnimator2 ragdoll = FindRagdollAnimator(hitCollider);

        if (ragdoll == null)
        {
            ConsumeNonRagdollHit();
            return;
        }

        consumedHit = true;

        Vector3 impact = ResolveImpactVector(relativeVelocity);

        if (disableColliderAfterHit && impactCollider != null)
            impactCollider.enabled = false;

        if (stopProjectileAfterHit && rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        if (hideRendererAfterHit)
            SetRenderersVisible(false);

        if (drawImpactDebug)
            Debug.DrawRay(hitPoint, impact, impactDebugColor, impactDebugDuration, false);

        if (logHit)
        {
            Debug.Log(
                $"[BulletImpact] Hit={hitCollider.name}, Ragdoll={ragdoll.name}, Impact={impact.magnitude}",
                this
            );
        }

        if (impactRoutine != null)
            StopCoroutine(impactRoutine);

        impactRoutine = StartCoroutine(ApplyImpactRoutine(ragdoll, impact));
    }

    private IEnumerator ApplyImpactRoutine(RagdollAnimator2 ragdoll, Vector3 impact)
    {
        if (ragdoll == null)
        {
            if (destroyOnRagdollHit)
                Destroy(gameObject);

            yield break;
        }

        if (!ragdoll.enabled && enableRagdollComponentIfDisabled)
            ragdoll.enabled = true;

        ragdoll.RagdollBlend = Mathf.Clamp01(targetRagdollBlend);

        if (switchToFall)
            ragdoll.RA2Event_SwitchToFall();

        if (notifyGetUpController)
            ragdoll.SendMessageUpwards("EnterRagdoll", SendMessageOptions.DontRequireReceiver);

        int repeatCount = Mathf.Max(1, impactFixedUpdateRepeats);

        for (int i = 0; i < repeatCount; i++)
        {
            if (ragdoll == null)
                break;

            float t = repeatCount <= 1 ? 1f : i / (float)(repeatCount - 1);
            float weight = impactFalloff != null ? impactFalloff.Evaluate(t) : 1f;

            ragdoll.RagdollBlend = Mathf.Clamp01(targetRagdollBlend);

            if (switchToFall)
                ragdoll.RA2Event_SwitchToFall();

            Vector3 weightedImpact = impact * Mathf.Max(0f, weight);

            if (addFullImpact)
                ragdoll.RA2Event_AddFullImpact(weightedImpact);

            if (addCoreImpact)
                ragdoll.RA2Event_AddCoreImpact(weightedImpact);

            yield return new WaitForFixedUpdate();
        }

        impactRoutine = null;

        if (destroyOnRagdollHit)
            Destroy(gameObject);
    }

    private void ConsumeNonRagdollHit()
    {
        if (!destroyOnNonRagdollHit)
            return;

        consumedHit = true;
        Destroy(gameObject);
    }

    private Vector3 ResolveImpactVector(Vector3 relativeVelocity)
    {
        Vector3 sourceVelocity = Vector3.zero;

        if (useStoredVelocityDirection && lastVelocity.sqrMagnitude > 0.0001f)
            sourceVelocity = lastVelocity;
        else if (relativeVelocity.sqrMagnitude > 0.0001f)
            sourceVelocity = relativeVelocity;
        else if (rb != null && rb.linearVelocity.sqrMagnitude > 0.0001f)
            sourceVelocity = rb.linearVelocity;
        else
            sourceVelocity = fireDirection * minimumImpactMagnitude;

        Vector3 dir = sourceVelocity.sqrMagnitude > 0.0001f
            ? sourceVelocity.normalized
            : fireDirection;

        dir.y += upwardImpact;

        if (dir.sqrMagnitude <= 0.0001f)
            dir = fireDirection;

        dir.Normalize();

        float magnitude;

        if (useVelocityMagnitudeForImpact)
            magnitude = sourceVelocity.magnitude * Mathf.Max(0f, velocityImpactMultiplier);
        else
            magnitude = Mathf.Max(0f, fixedImpactPower);

        magnitude = Mathf.Clamp(
            magnitude,
            Mathf.Max(0f, minimumImpactMagnitude),
            Mathf.Max(minimumImpactMagnitude, maximumImpactMagnitude)
        );

        return dir * magnitude;
    }

    private RagdollAnimator2 FindRagdollAnimator(Collider hitCollider)
    {
        if (hitCollider == null)
            return null;

        RagdollAnimator2 ragdoll = searchInParents
            ? hitCollider.GetComponentInParent<RagdollAnimator2>()
            : hitCollider.GetComponent<RagdollAnimator2>();

        if (ragdoll != null)
            return ragdoll;

        if (!alsoSearchThroughEnemyHealth)
            return null;

        EnemyHealth enemyHealth = searchInParents
            ? hitCollider.GetComponentInParent<EnemyHealth>()
            : hitCollider.GetComponent<EnemyHealth>();

        if (enemyHealth == null)
            return null;

        ragdoll = enemyHealth.GetComponent<RagdollAnimator2>();

        if (ragdoll != null)
            return ragdoll;

        return enemyHealth.GetComponentInChildren<RagdollAnimator2>(true);
    }

    private void IgnoreOwnerColliders()
    {
        if (!ignoreOwnerColliders)
            return;

        if (ownerRoot == null || impactCollider == null)
            return;

        Collider[] ownerColliders = ownerRoot.GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < ownerColliders.Length; i++)
        {
            Collider col = ownerColliders[i];

            if (col == null)
                continue;

            if (col == impactCollider)
                continue;

            Physics.IgnoreCollision(impactCollider, col, true);
        }
    }

    private bool ShouldIgnoreCollider(Collider col)
    {
        if (col == null)
            return true;

        if (!ignoreOwnerColliders)
            return false;

        if (ownerRoot == null)
            return false;

        return col.transform == ownerRoot || col.transform.IsChildOf(ownerRoot);
    }

    private bool IsLayerIncluded(LayerMask mask, int layer)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    private void SetRenderersVisible(bool visible)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            renderers[i].enabled = visible;
        }
    }
}