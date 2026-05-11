using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using FIMSpace.FProceduralAnimation;

[DisallowMultipleComponent]
public class EnemyRagdollGetUp : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private RagdollAnimator2 ragdollAnimator;
    [SerializeField] private Animator animator;
    [SerializeField] private NavMeshAgent navMeshAgent;
    [SerializeField] private MonoBehaviour[] scriptsToDisableWhileRagdolled;

    [Header("Ragdoll")]
    [SerializeField] private bool forceBlendToFullOnEnter = true;
    [SerializeField] private bool switchToFallOnEnter = true;

    [Header("Death Lock")]
    [SerializeField] private bool deathLocked = false;
    [SerializeField] private bool forceFullBlendWhileDeathLocked = true;

    [Header("Behavior Blackboard State")]
    [SerializeField] private bool useGettingUpBehaviorWindow = true;

    [Tooltip("Only affects behavior blackboard IsGettingUp. It does not affect animator, ragdoll, navmesh, or get-up timing.")]
    [Min(0f)]
    [SerializeField] private float gettingUpBehaviorDuration = 1.0f;

    [SerializeField] private bool markGettingUpOnForceStandNow = true;

    [Header("Root Sync Before Get Up")]
    [SerializeField] private Transform rootToMove;
    [SerializeField] private Transform getUpRootTarget;
    [SerializeField] private bool autoUseAnimatorHipsAsRootTarget = true;
    [SerializeField] private bool useRagdollBaseTransformIfNoTarget = true;
    [SerializeField] private bool syncRootPositionBeforeGetUp = true;
    [SerializeField] private bool syncRootRotationBeforeGetUp = false;
    [SerializeField] private bool snapRootToGround = true;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundRayStartHeight = 1.5f;
    [SerializeField] private float groundRayDistance = 5f;
    [SerializeField] private float groundOffset = 0f;
    [SerializeField] private bool keepOriginalRootYIfNoGround = false;

    [Header("Get Up Timing")]
    [SerializeField] private bool autoGetUp = true;
    [SerializeField] private float getUpDelay = 3f;
    [SerializeField] private float standTransitionDuration = 0.75f;
    [SerializeField] private float blendBackToAnimationDuration = 0.8f;

    [Header("Animator")]
    [SerializeField] private bool setGetUpTrigger = false;
    [SerializeField] private string getUpTriggerName = "GetUp";

    [Header("Control")]
    [SerializeField] private bool disableNavMeshAgentWhileRagdolled = true;
    [SerializeField] private bool enableNavMeshAgentAfterGetUp = true;
    [SerializeField] private bool warpNavMeshAgentAfterRootSync = true;
    [SerializeField] private bool disableScriptsWhileRagdolled = true;

    [Header("Debug")]
    [SerializeField] private bool logState = false;
    [SerializeField] private bool drawRootSyncDebug = false;
    [SerializeField] private Color rootSyncDebugColor = Color.cyan;
    [SerializeField] private float rootSyncDebugDuration = 1f;

    private Coroutine getUpRoutine;
    private bool isRagdolled;
    private int getUpTriggerHash;

    private float gettingUpEndTime;

    public bool IsRagdolled => isRagdolled;
    public bool DeathLocked => deathLocked;

    public bool IsGettingUp
    {
        get
        {
            return !deathLocked &&
                   useGettingUpBehaviorWindow &&
                   Time.time < gettingUpEndTime;
        }
    }

    public float GettingUpRemaining
    {
        get
        {
            return IsGettingUp
                ? Mathf.Max(0f, gettingUpEndTime - Time.time)
                : 0f;
        }
    }

    public float GettingUpBehaviorDuration => Mathf.Max(0f, gettingUpBehaviorDuration);

    private void Reset()
    {
        rootToMove = transform;
        ragdollAnimator = GetComponent<RagdollAnimator2>();
        animator = GetComponentInChildren<Animator>();
        navMeshAgent = GetComponent<NavMeshAgent>();

        TryAutoAssignHipsTarget();
    }

    private void Awake()
    {
        if (rootToMove == null)
            rootToMove = transform;

        if (ragdollAnimator == null)
            ragdollAnimator = GetComponent<RagdollAnimator2>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (navMeshAgent == null)
            navMeshAgent = GetComponent<NavMeshAgent>();

        if (getUpRootTarget == null && autoUseAnimatorHipsAsRootTarget)
            TryAutoAssignHipsTarget();

        getUpTriggerHash = Animator.StringToHash(getUpTriggerName);
    }

    private void OnDisable()
    {
        StopGetUpRoutineOnly();
        ClearGettingUpBehaviorWindow();
    }

    private void LateUpdate()
    {
        if (!deathLocked)
            return;

        if (!forceFullBlendWhileDeathLocked)
            return;

        if (ragdollAnimator == null)
            return;

        if (!ragdollAnimator.enabled)
            ragdollAnimator.enabled = true;

        if (ragdollAnimator.RagdollBlend < 0.999f)
            ragdollAnimator.RagdollBlend = 1f;
    }

    public void EnterRagdoll()
    {
        if (deathLocked)
            return;

        if (ragdollAnimator == null)
            return;

        isRagdolled = true;
        ClearGettingUpBehaviorWindow();

        if (!ragdollAnimator.enabled)
            ragdollAnimator.enabled = true;

        if (forceBlendToFullOnEnter)
            ragdollAnimator.RagdollBlend = 1f;

        if (switchToFallOnEnter)
            ragdollAnimator.RA2Event_SwitchToFall();

        if (disableNavMeshAgentWhileRagdolled && navMeshAgent != null)
            navMeshAgent.enabled = false;

        if (disableScriptsWhileRagdolled)
            SetExtraScriptsEnabled(false);

        StopGetUpRoutineOnly();

        if (autoGetUp)
            getUpRoutine = StartCoroutine(GetUpRoutine());

        if (logState)
            Debug.Log("[EnemyRagdollGetUp] Enter ragdoll.", this);
    }

    public void LockAsDeathRagdoll()
    {
        deathLocked = true;
        autoGetUp = false;
        isRagdolled = true;
        ClearGettingUpBehaviorWindow();

        StopGetUpRoutineOnly();

        if (ragdollAnimator != null)
        {
            if (!ragdollAnimator.enabled)
                ragdollAnimator.enabled = true;

            ragdollAnimator.RagdollBlend = 1f;
        }

        if (disableNavMeshAgentWhileRagdolled && navMeshAgent != null)
            navMeshAgent.enabled = false;

        if (disableScriptsWhileRagdolled)
            SetExtraScriptsEnabled(false);

        if (logState)
            Debug.Log("[EnemyRagdollGetUp] Locked as death ragdoll.", this);
    }

    public void UnlockDeathRagdoll()
    {
        deathLocked = false;
    }

    public void StartGetUp()
    {
        if (deathLocked)
            return;

        StopGetUpRoutineOnly();
        getUpRoutine = StartCoroutine(GetUpRoutine());
    }

    public void ForceStandNow()
    {
        if (deathLocked)
            return;

        StopGetUpRoutineOnly();

        if (markGettingUpOnForceStandNow)
            StartGettingUpBehaviorWindow();

        SyncRootToRagdollPosition();

        if (ragdollAnimator != null)
        {
            ragdollAnimator.RA2Event_SwitchToStand();
            ragdollAnimator.RagdollBlend = 0f;
        }

        RestoreControlAfterGetUp();

        isRagdolled = false;
    }

    private void StartGettingUpBehaviorWindow()
    {
        if (!useGettingUpBehaviorWindow)
            return;

        float duration = Mathf.Max(0f, gettingUpBehaviorDuration);

        if (duration <= 0f)
        {
            gettingUpEndTime = 0f;
            return;
        }

        gettingUpEndTime = Time.time + duration;

        if (logState)
        {
            Debug.Log(
                $"[EnemyRagdollGetUp] IsGettingUp behavior window started. Duration={duration:F2}s",
                this
            );
        }
    }

    private void ClearGettingUpBehaviorWindow()
    {
        gettingUpEndTime = 0f;
    }

    private void StopGetUpRoutineOnly()
    {
        if (getUpRoutine == null)
            return;

        StopCoroutine(getUpRoutine);
        getUpRoutine = null;
    }

    private IEnumerator GetUpRoutine()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, getUpDelay));

        if (deathLocked)
        {
            getUpRoutine = null;
            yield break;
        }

        if (ragdollAnimator == null)
            yield break;

        StartGettingUpBehaviorWindow();

        SyncRootToRagdollPosition();

        if (setGetUpTrigger && animator != null)
        {
            animator.ResetTrigger(getUpTriggerHash);
            animator.SetTrigger(getUpTriggerHash);
        }

        ragdollAnimator.RA2Event_TransitionStand(Mathf.Max(0.01f, standTransitionDuration));

        float startBlend = ragdollAnimator.RagdollBlend;
        float timer = 0f;
        float duration = Mathf.Max(0.01f, blendBackToAnimationDuration);

        while (timer < duration)
        {
            if (deathLocked)
            {
                ragdollAnimator.RagdollBlend = 1f;
                getUpRoutine = null;
                yield break;
            }

            timer += Time.deltaTime;

            float t = Mathf.Clamp01(timer / duration);
            ragdollAnimator.RagdollBlend = Mathf.Lerp(startBlend, 0f, t);

            yield return null;
        }

        if (!deathLocked)
            ragdollAnimator.RagdollBlend = 0f;

        RestoreControlAfterGetUp();

        isRagdolled = false;
        getUpRoutine = null;

        if (logState)
            Debug.Log("[EnemyRagdollGetUp] Finished get up.", this);
    }

    private void SyncRootToRagdollPosition()
    {
        if (!syncRootPositionBeforeGetUp && !syncRootRotationBeforeGetUp)
            return;

        if (rootToMove == null)
            rootToMove = transform;

        Transform target = ResolveRootSyncTarget();

        if (target == null || rootToMove == null)
            return;

        Vector3 oldRootPosition = rootToMove.position;
        Quaternion oldRootRotation = rootToMove.rotation;

        Vector3 targetPosition = target.position;
        Vector3 newRootPosition = rootToMove.position;

        if (syncRootPositionBeforeGetUp)
        {
            newRootPosition.x = targetPosition.x;
            newRootPosition.z = targetPosition.z;

            if (snapRootToGround)
            {
                Vector3 rayStart = targetPosition + Vector3.up * Mathf.Max(0f, groundRayStartHeight);

                if (Physics.Raycast(
                    rayStart,
                    Vector3.down,
                    out RaycastHit groundHit,
                    Mathf.Max(0.01f, groundRayDistance),
                    groundMask,
                    QueryTriggerInteraction.Ignore))
                {
                    newRootPosition.y = groundHit.point.y + groundOffset;
                }
                else if (!keepOriginalRootYIfNoGround)
                {
                    newRootPosition.y = targetPosition.y;
                }
            }
            else if (!keepOriginalRootYIfNoGround)
            {
                newRootPosition.y = targetPosition.y;
            }
        }

        Quaternion newRootRotation = rootToMove.rotation;

        if (syncRootRotationBeforeGetUp)
        {
            Vector3 forward = target.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude > 0.0001f)
                newRootRotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        rootToMove.SetPositionAndRotation(newRootPosition, newRootRotation);

        if (warpNavMeshAgentAfterRootSync)
            TryWarpNavMeshAgent(newRootPosition);

        if (drawRootSyncDebug)
        {
            Debug.DrawLine(oldRootPosition, newRootPosition, rootSyncDebugColor, rootSyncDebugDuration, false);
            Debug.DrawRay(newRootPosition, Vector3.up * 1.2f, rootSyncDebugColor, rootSyncDebugDuration, false);
        }

        if (logState)
        {
            Debug.Log(
                $"[EnemyRagdollGetUp] Root synced from {oldRootPosition} to {newRootPosition}. Rotation changed={oldRootRotation != newRootRotation}",
                this
            );
        }
    }

    private Transform ResolveRootSyncTarget()
    {
        if (getUpRootTarget != null)
            return getUpRootTarget;

        if (autoUseAnimatorHipsAsRootTarget && animator != null && animator.isHuman)
        {
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);

            if (hips != null)
            {
                getUpRootTarget = hips;
                return getUpRootTarget;
            }
        }

        if (useRagdollBaseTransformIfNoTarget && ragdollAnimator != null)
            return ragdollAnimator.GetBaseTransform;

        return null;
    }

    private void TryAutoAssignHipsTarget()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (animator == null)
            return;

        if (!animator.isHuman)
            return;

        Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);

        if (hips != null)
            getUpRootTarget = hips;
    }

    private void RestoreControlAfterGetUp()
    {
        if (enableNavMeshAgentAfterGetUp && navMeshAgent != null)
        {
            if (!navMeshAgent.enabled)
                navMeshAgent.enabled = true;

            if (warpNavMeshAgentAfterRootSync)
                navMeshAgent.Warp(rootToMove != null ? rootToMove.position : transform.position);
        }

        if (disableScriptsWhileRagdolled)
            SetExtraScriptsEnabled(true);
    }

    private void TryWarpNavMeshAgent(Vector3 position)
    {
        if (navMeshAgent == null)
            return;

        if (!navMeshAgent.enabled)
            return;

        navMeshAgent.Warp(position);
    }

    private void SetExtraScriptsEnabled(bool enabled)
    {
        if (scriptsToDisableWhileRagdolled == null)
            return;

        for (int i = 0; i < scriptsToDisableWhileRagdolled.Length; i++)
        {
            MonoBehaviour script = scriptsToDisableWhileRagdolled[i];

            if (script == null)
                continue;

            if (script == this)
                continue;

            script.enabled = enabled;
        }
    }
}