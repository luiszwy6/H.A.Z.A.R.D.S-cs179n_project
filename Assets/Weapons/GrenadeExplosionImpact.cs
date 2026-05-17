using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using FIMSpace.FProceduralAnimation;

namespace ASGS.Grenade
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SphereCollider))]
    [RequireComponent(typeof(Rigidbody))]
    public class GrenadeExplosionImpact : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private SphereCollider impactCollider;
        [SerializeField] private Rigidbody rb;

        [Header("Expansion")]
        [FormerlySerializedAs("startRadius")]
        [SerializeField] private float impactStartRadius = 0.05f;

        [FormerlySerializedAs("endRadius")]
        [SerializeField] private float impactEndRadius = 4f;

        [FormerlySerializedAs("expandDuration")]
        [SerializeField] private float impactExpandDuration = 0.08f;

        [SerializeField] private LayerMask impactMask = ~0;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

        [Header("Ragdoll Animator 2")]
        [SerializeField] private bool enableRagdollComponentIfDisabled = true;
        [SerializeField] private float targetRagdollBlend = 1f;
        [SerializeField] private bool switchToFall = true;
        [SerializeField] private bool notifyGetUpController = true;

        [Header("Impact")]
        [SerializeField] private float fixedImpactPower = 65f;
        [SerializeField] private float upwardImpact = 0.35f;
        [SerializeField] private bool addFullImpact = true;
        [SerializeField] private bool addCoreImpact = false;
        [SerializeField] private int impactFixedUpdateRepeats = 2;

        [Header("Gizmos")]
        [SerializeField] private bool drawImpactGizmos = true;
        [SerializeField] private bool drawImpactGizmosOnlyWhenSelected = true;
        [SerializeField] private Color impactStartRadiusColor = new Color(0f, 0.7f, 1f, 0.35f);
        [SerializeField] private Color impactEndRadiusColor = new Color(0f, 1f, 1f, 0.25f);

        [Header("Debug")]
        [SerializeField] private bool logImpact = false;

        private readonly HashSet<RagdollAnimator2> impactedRagdolls = new HashSet<RagdollAnimator2>();
        private Coroutine expandRoutine;

        private void Reset()
        {
            impactCollider = GetComponent<SphereCollider>();
            rb = GetComponent<Rigidbody>();

            if (impactCollider != null)
            {
                impactCollider.isTrigger = true;
                impactCollider.radius = Mathf.Max(0.01f, impactStartRadius);
            }

            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }

        private void Awake()
        {
            if (impactCollider == null)
                impactCollider = GetComponent<SphereCollider>();

            if (rb == null)
                rb = GetComponent<Rigidbody>();

            if (impactCollider != null)
            {
                impactCollider.isTrigger = true;
                impactCollider.radius = Mathf.Max(0.01f, impactStartRadius);
                impactCollider.enabled = false;
            }

            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }

        public void Activate(Vector3 explosionPosition)
        {
            transform.position = explosionPosition;
            gameObject.SetActive(true);

            impactedRagdolls.Clear();

            if (expandRoutine != null)
                StopCoroutine(expandRoutine);

            expandRoutine = StartCoroutine(ExpandRoutine(explosionPosition));
        }

        private IEnumerator ExpandRoutine(Vector3 explosionPosition)
        {
            if (impactCollider == null)
                yield break;

            impactCollider.enabled = true;
            impactCollider.radius = Mathf.Max(0.01f, impactStartRadius);

            float timer = 0f;
            float duration = Mathf.Max(0.001f, impactExpandDuration);
            float start = Mathf.Max(0.01f, impactStartRadius);
            float end = Mathf.Max(start, impactEndRadius);

            while (timer < duration)
            {
                timer += Time.deltaTime;

                float t = Mathf.Clamp01(timer / duration);
                float radius = Mathf.Lerp(start, end, t);

                impactCollider.radius = radius;

                ProcessOverlap(explosionPosition, radius);

                yield return null;
            }

            impactCollider.radius = end;
            ProcessOverlap(explosionPosition, end);

            yield return new WaitForFixedUpdate();

            impactCollider.enabled = false;
            expandRoutine = null;
        }

        private void ProcessOverlap(Vector3 explosionPosition, float radius)
        {
            Collider[] hits = Physics.OverlapSphere(
                explosionPosition,
                Mathf.Max(0.01f, radius),
                impactMask,
                triggerInteraction
            );

            for (int i = 0; i < hits.Length; i++)
            {
                Collider hit = hits[i];

                if (hit == null)
                    continue;

                RagdollAnimator2 ragdoll = FindRagdollAnimator(hit);

                if (ragdoll == null)
                    continue;

                if (impactedRagdolls.Contains(ragdoll))
                    continue;

                impactedRagdolls.Add(ragdoll);

                Vector3 impact = ResolveImpactVector(explosionPosition, ragdoll.transform.position);
                StartCoroutine(ApplyImpactRoutine(ragdoll, impact));

                if (logImpact)
                    Debug.Log($"[GrenadeExplosionImpact] Impact ragdoll={ragdoll.name}, Impact={impact.magnitude}", this);
            }
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

            int repeats = Mathf.Max(1, impactFixedUpdateRepeats);

            for (int i = 0; i < repeats; i++)
            {
                if (ragdoll == null)
                    yield break;

                ragdoll.RagdollBlend = Mathf.Clamp01(targetRagdollBlend);

                if (switchToFall)
                    ragdoll.RA2Event_SwitchToFall();

                if (addFullImpact)
                    ragdoll.RA2Event_AddFullImpact(impact);

                if (addCoreImpact)
                    ragdoll.RA2Event_AddCoreImpact(impact);

                yield return new WaitForFixedUpdate();
            }
        }

        private Vector3 ResolveImpactVector(Vector3 explosionPosition, Vector3 targetPosition)
        {
            Vector3 dir = targetPosition - explosionPosition;
            dir.y += upwardImpact;

            if (dir.sqrMagnitude <= 0.0001f)
                dir = Vector3.up;

            dir.Normalize();

            return dir * Mathf.Max(0f, fixedImpactPower);
        }

        private RagdollAnimator2 FindRagdollAnimator(Collider hitCollider)
        {
            if (hitCollider == null)
                return null;

            RagdollAnimator2 ragdoll = hitCollider.GetComponentInParent<RagdollAnimator2>();

            if (ragdoll != null)
                return ragdoll;

            EnemyHealth enemyHealth = hitCollider.GetComponentInParent<EnemyHealth>();

            if (enemyHealth == null)
                return null;

            ragdoll = enemyHealth.GetComponent<RagdollAnimator2>();

            if (ragdoll != null)
                return ragdoll;

            return enemyHealth.GetComponentInChildren<RagdollAnimator2>(true);
        }

        private void OnDrawGizmos()
        {
            if (!drawImpactGizmos)
                return;

            if (drawImpactGizmosOnlyWhenSelected)
                return;

            DrawImpactGizmos();
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawImpactGizmos)
                return;

            DrawImpactGizmos();
        }

        private void DrawImpactGizmos()
        {
            Vector3 center = transform.position;

            float start = Mathf.Max(0f, impactStartRadius);
            float end = Mathf.Max(start, impactEndRadius);

            if (end > 0f)
            {
                Gizmos.color = impactEndRadiusColor;
                Gizmos.DrawWireSphere(center, end);
            }

            if (start > 0f)
            {
                Gizmos.color = impactStartRadiusColor;
                Gizmos.DrawWireSphere(center, start);
            }
        }
    }
}