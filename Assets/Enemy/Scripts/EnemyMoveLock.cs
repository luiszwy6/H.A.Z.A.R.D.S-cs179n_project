using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class EnemyMovementLockController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private EnemyAnimatorParameterDriver animatorDriver;

    [Header("Lock Settings")]
    [SerializeField] private bool forceZeroVelocity = true;
    [SerializeField] private bool restoreAgentRotationOnUnlock = true;
    [SerializeField] private bool restoreStoppedStateOnUnlock = true;
    [SerializeField] private EnemyMoveMode lockedMoveMode = EnemyMoveMode.None;

    private int lockCount;

    private bool cachedIsStopped;
    private bool cachedUpdateRotation;
    private EnemyMoveMode cachedMoveMode;

    private bool hasCachedAgentState;

    public bool IsMovementLocked
    {
        get { return lockCount > 0; }
    }

    private void Awake()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (animatorDriver == null)
            animatorDriver = GetComponent<EnemyAnimatorParameterDriver>();
    }

    private void LateUpdate()
    {
        if (!IsMovementLocked)
            return;

        ApplyLock();
    }

    public void AddMovementLock()
    {
        lockCount++;

        if (lockCount == 1)
        {
            CacheAgentState();
            ApplyLock();
        }
    }

    public void RemoveMovementLock()
    {
        lockCount = Mathf.Max(0, lockCount - 1);

        if (lockCount == 0)
            RestoreAgentState();
    }

    public void ClearMovementLocks()
    {
        lockCount = 0;
        RestoreAgentState();
    }

    private void ApplyLock()
    {
        if (agent != null)
        {
            // This pauses movement but keeps the current path and destination.
            agent.isStopped = true;

            if (forceZeroVelocity)
                agent.velocity = Vector3.zero;
        }

        if (animatorDriver != null)
            animatorDriver.SetMoveMode(lockedMoveMode);
    }

    private void CacheAgentState()
    {
        if (hasCachedAgentState)
            return;

        if (agent != null)
        {
            cachedIsStopped = agent.isStopped;
            cachedUpdateRotation = agent.updateRotation;
        }

        if (animatorDriver != null)
            cachedMoveMode = animatorDriver.GetMoveMode();

        hasCachedAgentState = true;
    }

    private void RestoreAgentState()
    {
        if (!hasCachedAgentState)
            return;

        if (agent != null)
        {
            if (restoreStoppedStateOnUnlock)
                agent.isStopped = cachedIsStopped;

            if (restoreAgentRotationOnUnlock)
                agent.updateRotation = cachedUpdateRotation;
        }

        if (animatorDriver != null)
            animatorDriver.SetMoveMode(cachedMoveMode);

        hasCachedAgentState = false;
    }
}