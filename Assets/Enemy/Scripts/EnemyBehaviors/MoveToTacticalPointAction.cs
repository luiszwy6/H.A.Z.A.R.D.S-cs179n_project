using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using UnityEngine.AI;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Move To Tactical Move Point",
    story: "[Self] moves to [TacticalMovePoint] using [MoveMode]",
    category: "Enemy/Movement",
    id: "a4c6f1c65b264c4f9e4e0e0f3c7f1a92"
)]
public partial class MoveToTacticalMovePointAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<Transform> TacticalMovePoint;

    [SerializeReference] public BlackboardVariable<float> StopDistance;
    [SerializeReference] public BlackboardVariable<EnemyMoveMode> MoveMode;
    [SerializeReference] public BlackboardVariable<bool> HasTacticalMovePoint;

    [Header("Options")]
    [SerializeField] private bool resetPathOnSuccess = true;
    [SerializeField] private bool failIfPointMissing = true;
    [SerializeField] private bool waitForTacticalMovePointReleasedOnReach = false;

    [Header("Interrupt")]
    [SerializeField] private bool abortIfCannotSeeTarget = true;

    private NavMeshAgent agent;
    private EnemyAnimatorParameterDriver animatorDriver;
    private EnemyStatus enemyStatus;

    protected override Status OnStart()
    {
        if (Self == null || Self.Value == null)
            return Status.Failure;

        agent = Self.Value.GetComponent<NavMeshAgent>();
        animatorDriver = Self.Value.GetComponent<EnemyAnimatorParameterDriver>();
        enemyStatus = Self.Value.GetComponent<EnemyStatus>();

        if (agent == null)
            return Status.Failure;

        if (TacticalMovePoint == null || TacticalMovePoint.Value == null)
            return failIfPointMissing ? Status.Failure : Status.Success;

        EnemyMoveMode moveMode = ResolveMoveMode();

        if (animatorDriver != null)
            animatorDriver.SetMoveMode(moveMode);

        agent.isStopped = false;
        agent.SetDestination(TacticalMovePoint.Value.position);

        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return Status.Failure;

        if (Self == null || Self.Value == null)
            return Status.Failure;

        if (abortIfCannotSeeTarget)
        {
            enemyStatus ??= Self.Value.GetComponent<EnemyStatus>();

            if (enemyStatus != null && !enemyStatus.CanSeeTarget)
                return Status.Failure;
        }

        if (TacticalMovePoint == null || TacticalMovePoint.Value == null)
            return failIfPointMissing ? Status.Failure : Status.Success;

        float stopDistance = StopDistance != null ? StopDistance.Value : 0.75f;
        Vector3 destination = TacticalMovePoint.Value.position;

        agent.isStopped = false;

        if (!agent.pathPending)
        {
            float distance = Vector3.Distance(
                Self.Value.transform.position,
                destination
            );

            if (distance <= stopDistance)
            {
                agent.isStopped = true;

                if (resetPathOnSuccess)
                    agent.ResetPath();

                if (ShouldWaitForTacticalMovePointRelease())
                    return Status.Running;

                return Status.Success;
            }
        }

        if (!agent.hasPath || Vector3.Distance(agent.destination, destination) > 0.25f)
        {
            agent.SetDestination(destination);
        }

        return Status.Running;
    }

    protected override void OnEnd()
    {
    }

    private EnemyMoveMode ResolveMoveMode()
    {
        if (MoveMode == null)
            return EnemyMoveMode.Run;

        return MoveMode.Value;
    }

    private bool ShouldWaitForTacticalMovePointRelease()
    {
        if (!waitForTacticalMovePointReleasedOnReach)
            return false;

        if (HasTacticalMovePoint == null)
            return false;

        return HasTacticalMovePoint.Value;
    }
}
