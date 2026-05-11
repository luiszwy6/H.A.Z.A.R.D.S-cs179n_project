using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FIMSpace.FProceduralAnimation;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class MeleeImpact : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Rigidbody impactRigidbody;
    [SerializeField] private Collider impactCollider;

    [Header("Owner Ignore")]
    [SerializeField] private Transform ownerRoot;
    [SerializeField] private bool ignoreOwnerColliders = true;
    [SerializeField] private bool ignoreOwnerOnAwake = true;

    [Header("Impact Window")]
    [SerializeField] private bool disableColliderOnAwake = true;
    [SerializeField] private bool enableColliderDuringImpactWindow = true;
    [SerializeField] private bool impactWindowActiveOnEnable = false;
    [SerializeField] private float defaultImpactWindowDuration = 0.18f;

    [Header("Target")]
    [SerializeField] private LayerMask ragdollHitMask = ~0;
    [SerializeField] private bool requireTargetLayer = false;
    [SerializeField] private bool searchInParents = true;
    [SerializeField] private bool alsoSearchThroughEnemyHealth = true;

    [Header("Ragdoll Animator 2")]
    [SerializeField] private bool enableRagdollComponentIfDisabled = true;
    [SerializeField] private float targetRagdollBlend = 1f;
    [SerializeField] private bool switchToFall = true;
    [SerializeField] private bool notifyGetUpController = true;

    [Header("Impact Direction")]
    [SerializeField] private bool useOwnerToTargetDirection = true;
    [SerializeField] private Transform fallbackForwardSource;
    [SerializeField] private bool flattenImpactDirection = true;
    [SerializeField] private float upwardImpact = 0.15f;

    [Header("Impact Power")]
    [SerializeField] private float fixedImpactPower = 45f;
    [SerializeField] private float minimumImpactMagnitude = 15f;
    [SerializeField] private float maximumImpactMagnitude = 120f;

    [Header("Impact Application")]
    [SerializeField] private bool addFullImpact = true;
    [SerializeField] private bool addCoreImpact = false;
    [SerializeField] private int impactFixedUpdateRepeats = 1;
    [SerializeField] private AnimationCurve impactFalloff = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Header("Hit Rules")]
    [SerializeField] private bool impactEachRagdollOncePerWindow = true;
    [SerializeField] private bool processTriggerStay = true;
    [SerializeField] private bool processCollisionStay = false;

    [Header("Rigidbody Setup")]
    [SerializeField] private bool setupRigidbodyOnAwake = true;
    [SerializeField] private bool rigidbodyIsKinematic = true;
    [SerializeField] private bool useGravity = false;
    [SerializeField] private CollisionDetectionMode collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    [SerializeField] private RigidbodyInterpolation interpolation = RigidbodyInterpolation.Interpolate;

    [Header("Debug")]
    [SerializeField] private bool logImpact = false;
    [SerializeField] private bool drawImpactDebug = false;
    [SerializeField] private Color impactDebugColor = Color.red;
    [SerializeField] private float impactDebugDuration = 0.6f;

    private readonly HashSet<RagdollAnimator2> impactedRagdollsThisWindow = new HashSet<RagdollAnimator2>();

    private Coroutine impactWindowRoutine;
    private bool impactWindowActive;

    private void Reset()
    {
        impactRigidbody = GetComponent<Rigidbody>();
        impactCollider = GetComponent<Collider>();

        ownerRoot = transform.root;
        fallbackForwardSource = transform;

        if (impactCollider != null)
            impactCollider.isTrigger = true;
    }

    private void Awake()
    {
        if (impactRigidbody == null)
            impactRigidbody = GetComponent<Rigidbody>();

        if (impactCollider == null)
            impactCollider = GetComponent<Collider>();

        if (ownerRoot == null)
            ownerRoot = transform.root;

        if (fallbackForwardSource == null)
            fallbackForwardSource = transform;

        SetupRigidbody();

        if (ignoreOwnerOnAwake)
            IgnoreOwnerColliders();

        if (disableColliderOnAwake && impactCollider != null)
            impactCollider.enabled = false;
    }

    private void OnEnable()
    {
        if (impactWindowActiveOnEnable)
            OpenImpactWindow();
    }

    private void OnDisable()
    {
        CloseImpactWindow();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!impactWindowActive)
            return;

        HandleHit(other, GetHitPoint(other));
    }

    private void OnTriggerStay(Collider other)
    {
        if (!processTriggerStay)
            return;

        if (!impactWindowActive)
            return;

        HandleHit(other, GetHitPoint(other));
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!impactWindowActive)
            return;

        if (collision == null)
            return;

        Collider other = collision.collider;

        Vector3 hitPoint = collision.contactCount > 0
            ? collision.GetContact(0).point
            : GetHitPoint(other);

        HandleHit(other, hitPoint);
    }

    private void OnCollisionStay(Collision collision)
    {
        if (!processCollisionStay)
            return;

        if (!impactWindowActive)
            return;

        if (collision == null)
            return;

        Collider other = collision.collider;

        Vector3 hitPoint = collision.contactCount > 0
            ? collision.GetContact(0).point
            : GetHitPoint(other);

        HandleHit(other, hitPoint);
    }

    public void OpenImpactWindow()
    {
        impactWindowActive = true;
        impactedRagdollsThisWindow.Clear();

        if (enableColliderDuringImpactWindow && impactCollider != null)
            impactCollider.enabled = true;
    }

    public void CloseImpactWindow()
    {
        impactWindowActive = false;
        impactedRagdollsThisWindow.Clear();

        if (impactWindowRoutine != null)
        {
            StopCoroutine(impactWindowRoutine);
            impactWindowRoutine = null;
        }

        if (enableColliderDuringImpactWindow && impactCollider != null)
            impactCollider.enabled = false;
    }

    public void OpenImpactWindowForDefaultDuration()
    {
        OpenImpactWindowForSeconds(defaultImpactWindowDuration);
    }

    public void OpenImpactWindowForSeconds(float seconds)
    {
        if (impactWindowRoutine != null)
        {
            StopCoroutine(impactWindowRoutine);
            impactWindowRoutine = null;
        }

        impactWindowRoutine = StartCoroutine(ImpactWindowRoutine(seconds));
    }

    public void EnableImpact()
    {
        OpenImpactWindow();
    }

    public void DisableImpact()
    {
        CloseImpactWindow();
    }

    private IEnumerator ImpactWindowRoutine(float seconds)
    {
        OpenImpactWindow();

        yield return new WaitForSeconds(Mathf.Max(0.01f, seconds));

        CloseImpactWindow();
        impactWindowRoutine = null;
    }

    private void SetupRigidbody()
    {
        if (!setupRigidbodyOnAwake)
            return;

        if (impactRigidbody == null)
            return;

        impactRigidbody.isKinematic = rigidbodyIsKinematic;
        impactRigidbody.useGravity = useGravity;
        impactRigidbody.collisionDetectionMode = collisionDetectionMode;
        impactRigidbody.interpolation = interpolation;
    }

    private void HandleHit(Collider hitCollider, Vector3 hitPoint)
    {
        if (hitCollider == null)
            return;

        if (hitCollider == impactCollider)
            return;

        if (ShouldIgnoreCollider(hitCollider))
            return;

        if (requireTargetLayer && !IsLayerIncluded(ragdollHitMask, hitCollider.gameObject.layer))
            return;

        RagdollAnimator2 ragdoll = FindRagdollAnimator(hitCollider);

        if (ragdoll == null)
            return;

        if (impactEachRagdollOncePerWindow && impactedRagdollsThisWindow.Contains(ragdoll))
            return;

        impactedRagdollsThisWindow.Add(ragdoll);

        Vector3 impact = ResolveImpactVector(hitCollider);

        if (drawImpactDebug)
            Debug.DrawRay(hitPoint, impact.normalized * Mathf.Min(impact.magnitude, 5f), impactDebugColor, impactDebugDuration, false);

        if (logImpact)
        {
            Debug.Log(
                $"[MeleeImpact] Hit={hitCollider.name}, Ragdoll={ragdoll.name}, Impact={impact.magnitude:F2}",
                this
            );
        }

        StartCoroutine(ApplyImpactRoutine(ragdoll, impact));
    }

    private IEnumerator ApplyImpactRoutine(RagdollAnimator2 ragdoll, Vector3 impact)
    {
        if (ragdoll == null)
            yield break;

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
    }

    private Vector3 ResolveImpactVector(Collider hitCollider)
    {
        Vector3 dir;

        if (useOwnerToTargetDirection && hitCollider != null)
        {
            Vector3 source = ownerRoot != null
                ? ownerRoot.position
                : transform.position;

            Vector3 target = hitCollider.bounds.center;
            dir = target - source;
        }
        else
        {
            Transform forwardSource = fallbackForwardSource != null
                ? fallbackForwardSource
                : transform;

            dir = forwardSource.forward;
        }

        if (flattenImpactDirection)
            dir.y = 0f;

        if (dir.sqrMagnitude <= 0.0001f)
        {
            Transform forwardSource = fallbackForwardSource != null
                ? fallbackForwardSource
                : transform;

            dir = forwardSource.forward;

            if (flattenImpactDirection)
                dir.y = 0f;
        }

        if (dir.sqrMagnitude <= 0.0001f)
            dir = Vector3.forward;

        dir.Normalize();

        if (upwardImpact > 0f)
            dir = (dir + Vector3.up * upwardImpact).normalized;

        float magnitude = Mathf.Clamp(
            Mathf.Max(0f, fixedImpactPower),
            Mathf.Max(0f, minimumImpactMagnitude),
            Mathf.Max(minimumImpactMagnitude, maximumImpactMagnitude)
        );

        return dir * magnitude;
    }

    private Vector3 GetHitPoint(Collider other)
    {
        if (other == null)
            return transform.position;

        if (impactCollider != null)
            return other.ClosestPoint(impactCollider.bounds.center);

        return other.ClosestPoint(transform.position);
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
}