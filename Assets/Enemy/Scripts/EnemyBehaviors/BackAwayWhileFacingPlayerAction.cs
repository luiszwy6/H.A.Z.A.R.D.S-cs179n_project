using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using UnityEngine.AI;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Back Away From Target",
    story: "[Self] backs away from [Target]",
    category: "Enemy/Movement",
    id: "f7f70cfb1fcb4e20bc63cd116bb65fc5"
)]
public partial class BackAwayWhileFacingPlayerAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> Target;

    [SerializeReference] public BlackboardVariable<float> SafeRange;
    [SerializeReference] public BlackboardVariable<float> BackAwayDistance;
    [SerializeReference] public BlackboardVariable<float> NavMeshSampleDistance;

    [SerializeReference] public BlackboardVariable<EnemyMoveMode> MoveMode;

    [SerializeReference] public BlackboardVariable<bool> DisableAgentRotation;
    [SerializeReference] public BlackboardVariable<bool> RestoreAgentRotationOnEnd;
    [SerializeReference] public BlackboardVariable<bool> ForceZeroVelocityWhenSafe;
    [SerializeReference] public BlackboardVariable<bool> ReturnRunningWhileActive;

    private NavMeshAgent agent;
    private EnemyAnimatorParameterDriver animatorDriver;

    private Vector3 retreatPosition;
    private bool hasRetreatPosition;

    protected override Status OnStart()
    {
        if (Self == null || Self.Value == null)
            return Status.Failure;

        if (Target == null || Target.Value == null)
            return Status.Failure;

        agent = Self.Value.GetComponent<NavMeshAgent>();
        animatorDriver = Self.Value.GetComponent<EnemyAnimatorParameterDriver>();

        if (agent == null)
            return Status.Failure;

        if (!agent.isOnNavMesh)
            return Status.Failure;

        if (animatorDriver != null)
            animatorDriver.SetMoveMode(ResolveMoveMode());

        if (ResolveDisableAgentRotation())
            agent.updateRotation = false;

        hasRetreatPosition = false;

        return TickBackAway();
    }

    protected override Status OnUpdate()
    {
        if (Self == null || Self.Value == null)
            return Status.Failure;

        if (Target == null || Target.Value == null)
            return Status.Failure;

        if (agent == null)
            return Status.Failure;

        if (!agent.isOnNavMesh)
            return Status.Failure;

        if (animatorDriver != null)
            animatorDriver.SetMoveMode(ResolveMoveMode());

        if (ResolveDisableAgentRotation())
            agent.updateRotation = false;

        return TickBackAway();
    }

    protected override void OnEnd()
    {
        hasRetreatPosition = false;

        if (agent != null && ResolveRestoreAgentRotationOnEnd())
            agent.updateRotation = true;
    }

    private Status TickBackAway()
    {
        float safeRange = ResolveSafeRange();

        float currentDistance = Vector3.Distance(
            Self.Value.transform.position,
            Target.Value.transform.position
        );

        if (currentDistance >= safeRange)
        {
            StopAgent();

            return Status.Success;
        }

        if (!hasRetreatPosition)
        {
            if (!CalculateRetreatPosition())
                return Status.Failure;

            SetRetreatDestination();
        }

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.2f)
        {
            if (!CalculateRetreatPosition())
                return Status.Failure;

            SetRetreatDestination();
        }

        return ResolveReturnRunningWhileActive() ? Status.Running : Status.Success;
    }

    private void SetRetreatDestination()
    {
        agent.isStopped = false;

        if (ResolveDisableAgentRotation())
            agent.updateRotation = false;

        agent.SetDestination(retreatPosition);
        hasRetreatPosition = true;
    }

    private void StopAgent()
    {
        if (agent == null)
            return;

        agent.isStopped = true;
        agent.ResetPath();

        if (ResolveForceZeroVelocityWhenSafe())
            agent.velocity = Vector3.zero;
    }

    private bool CalculateRetreatPosition()
    {
        Transform selfTransform = Self.Value.transform;
        Transform targetTransform = Target.Value.transform;

        Vector3 awayDirection = selfTransform.position - targetTransform.position;
        awayDirection.y = 0f;

        if (awayDirection.sqrMagnitude < 0.01f)
            awayDirection = -targetTransform.forward;

        float backAwayDistance = ResolveBackAwayDistance();

        Vector3 desiredPosition =
            selfTransform.position + awayDirection.normalized * backAwayDistance;

        float sampleDistance = ResolveNavMeshSampleDistance();

        if (NavMesh.SamplePosition(desiredPosition, out NavMeshHit hit, sampleDistance, NavMesh.AllAreas))
        {
            retreatPosition = hit.position;
            return true;
        }

        return false;
    }

    private EnemyMoveMode ResolveMoveMode()
    {
        if (MoveMode == null)
            return EnemyMoveMode.Walk;

        return MoveMode.Value;
    }

    private float ResolveSafeRange()
    {
        if (SafeRange == null)
            return 5f;

        return Mathf.Max(0f, SafeRange.Value);
    }

    private float ResolveBackAwayDistance()
    {
        if (BackAwayDistance == null)
            return 4f;

        return Mathf.Max(0f, BackAwayDistance.Value);
    }

    private float ResolveNavMeshSampleDistance()
    {
        if (NavMeshSampleDistance == null)
            return 2f;

        return Mathf.Max(0.1f, NavMeshSampleDistance.Value);
    }

    private bool ResolveDisableAgentRotation()
    {
        if (DisableAgentRotation == null)
            return true;

        return DisableAgentRotation.Value;
    }

    private bool ResolveRestoreAgentRotationOnEnd()
    {
        if (RestoreAgentRotationOnEnd == null)
            return false;

        return RestoreAgentRotationOnEnd.Value;
    }

    private bool ResolveForceZeroVelocityWhenSafe()
    {
        if (ForceZeroVelocityWhenSafe == null)
            return true;

        return ForceZeroVelocityWhenSafe.Value;
    }

    private bool ResolveReturnRunningWhileActive()
    {
        if (ReturnRunningWhileActive == null)
            return false;

        return ReturnRunningWhileActive.Value;
    }
}