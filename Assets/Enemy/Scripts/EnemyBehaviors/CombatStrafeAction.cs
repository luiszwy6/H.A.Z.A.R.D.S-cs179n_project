using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using UnityEngine.AI;
using Action = Unity.Behavior.Action;

public enum EnemyStrafeMovePattern
{
    LeftRightFromFacing,
    RandomPointAroundCenter
}

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Combat Strafe",
    story: "[Self] combat strafes using [MoveMode]",
    category: "Enemy/Movement",
    id: "a13b7f6f6a5f4a5c8b2e2c0f3c0f7b91"
)]
public partial class CombatStrafeAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;

    [SerializeReference] public BlackboardVariable<EnemyMoveMode> MoveMode;
    [SerializeReference] public BlackboardVariable<EnemyStrafeMovePattern> StrafePattern;

    [SerializeReference] public BlackboardVariable<float> MinStrafeDistance;
    [SerializeReference] public BlackboardVariable<float> MaxStrafeDistance;
    [SerializeReference] public BlackboardVariable<float> StrafeRadius;
    [SerializeReference] public BlackboardVariable<float> ReachDistance;

    [SerializeReference] public BlackboardVariable<float> MinWaitAtPointTime;
    [SerializeReference] public BlackboardVariable<float> MaxWaitAtPointTime;

    [SerializeReference] public BlackboardVariable<float> NavMeshSampleDistance;

    [SerializeReference] public BlackboardVariable<bool> DisableAgentRotation;
    [SerializeReference] public BlackboardVariable<bool> ResetCenterAfterReach;
    [SerializeReference] public BlackboardVariable<bool> ForceZeroVelocityAtPoint;
    [SerializeReference] public BlackboardVariable<bool> ReturnRunningWhileActive;

    private NavMeshAgent agent;
    private EnemyAnimatorParameterDriver animatorDriver;

    private GameObject cachedSelf;

    private Vector3 currentCenter;
    private Vector3 currentDestination;

    private bool hasCenter;
    private bool hasDestination;
    private bool isWaitingAtPoint;
    private float waitTimer;
    private float currentWaitAtPointTime;

    protected override Status OnStart()
    {
        if (Self == null || Self.Value == null)
            return Status.Failure;

        if (cachedSelf != Self.Value)
        {
            cachedSelf = Self.Value;
            ResetRuntimeState();
        }

        agent = Self.Value.GetComponent<NavMeshAgent>();
        animatorDriver = Self.Value.GetComponent<EnemyAnimatorParameterDriver>();

        if (agent == null)
            return Status.Failure;

        if (!agent.isOnNavMesh)
            return Status.Failure;

        if (!hasCenter)
        {
            currentCenter = Self.Value.transform.position;
            hasCenter = true;
        }

        if (animatorDriver != null)
            animatorDriver.SetMoveMode(ResolveMoveMode());

        if (ResolveDisableAgentRotation())
            agent.updateRotation = false;

        return TickStrafe();
    }

    protected override Status OnUpdate()
    {
        if (Self == null || Self.Value == null)
            return Status.Failure;

        if (agent == null)
            return Status.Failure;

        if (!agent.isOnNavMesh)
            return Status.Failure;

        if (animatorDriver != null)
            animatorDriver.SetMoveMode(ResolveMoveMode());

        if (ResolveDisableAgentRotation())
            agent.updateRotation = false;

        return TickStrafe();
    }

    protected override void OnEnd()
    {
    }

    private Status TickStrafe()
    {
        if (isWaitingAtPoint)
        {
            waitTimer += Time.deltaTime;

            if (waitTimer >= currentWaitAtPointTime)
            {
                isWaitingAtPoint = false;
                waitTimer = 0f;

                if (!PickNewDestination())
                    return Status.Failure;
            }

            return ResolveReturnRunningWhileActive() ? Status.Running : Status.Success;
        }

        if (!hasDestination)
        {
            if (!PickNewDestination())
                return Status.Failure;
        }

        if (!agent.pathPending && agent.remainingDistance <= ResolveReachDistance())
        {
            agent.isStopped = true;
            agent.ResetPath();

            if (ResolveForceZeroVelocityAtPoint())
                agent.velocity = Vector3.zero;

            hasDestination = false;
            isWaitingAtPoint = true;
            waitTimer = 0f;
            currentWaitAtPointTime = ResolveRandomWaitAtPointTime();

            if (ResolveResetCenterAfterReach())
                currentCenter = Self.Value.transform.position;
        }

        return ResolveReturnRunningWhileActive() ? Status.Running : Status.Success;
    }

    private bool PickNewDestination()
    {
        if (agent == null)
            return false;

        Vector3 candidate;

        if (ResolveStrafePattern() == EnemyStrafeMovePattern.LeftRightFromFacing)
            candidate = GetLeftRightCandidate();
        else
            candidate = GetRandomPointAroundCenterCandidate();

        float sampleDistance = ResolveNavMeshSampleDistance();

        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, sampleDistance, NavMesh.AllAreas))
        {
            currentDestination = hit.position;

            agent.isStopped = false;

            if (ResolveDisableAgentRotation())
                agent.updateRotation = false;

            agent.SetDestination(currentDestination);
            hasDestination = true;

            return true;
        }

        hasDestination = false;
        return false;
    }

    private Vector3 GetLeftRightCandidate()
    {
        Transform selfTransform = Self.Value.transform;

        float min = ResolveMinStrafeDistance();
        float max = ResolveMaxStrafeDistance();

        if (max < min)
            max = min;

        float distance = UnityEngine.Random.Range(min, max);
        float side = UnityEngine.Random.value < 0.5f ? -1f : 1f;

        Vector3 right = selfTransform.right;
        right.y = 0f;

        if (right.sqrMagnitude < 0.01f)
            right = Vector3.right;

        return selfTransform.position + right.normalized * side * distance;
    }

    private Vector3 GetRandomPointAroundCenterCandidate()
    {
        float radius = ResolveStrafeRadius();

        Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * radius;
        return currentCenter + new Vector3(randomCircle.x, 0f, randomCircle.y);
    }

    private void ResetRuntimeState()
    {
        hasCenter = false;
        hasDestination = false;
        isWaitingAtPoint = false;
        waitTimer = 0f;
        currentWaitAtPointTime = 0f;
        currentCenter = Vector3.zero;
        currentDestination = Vector3.zero;
    }

    private EnemyMoveMode ResolveMoveMode()
    {
        if (MoveMode == null)
            return EnemyMoveMode.Walk;

        return MoveMode.Value;
    }

    private EnemyStrafeMovePattern ResolveStrafePattern()
    {
        if (StrafePattern == null)
            return EnemyStrafeMovePattern.LeftRightFromFacing;

        return StrafePattern.Value;
    }

    private float ResolveMinStrafeDistance()
    {
        if (MinStrafeDistance == null)
            return 1.5f;

        return Mathf.Max(0f, MinStrafeDistance.Value);
    }

    private float ResolveMaxStrafeDistance()
    {
        if (MaxStrafeDistance == null)
            return 3.5f;

        return Mathf.Max(0f, MaxStrafeDistance.Value);
    }

    private float ResolveStrafeRadius()
    {
        if (StrafeRadius == null)
            return 3f;

        return Mathf.Max(0.1f, StrafeRadius.Value);
    }

    private float ResolveReachDistance()
    {
        if (ReachDistance == null)
            return 0.6f;

        return Mathf.Max(0.05f, ReachDistance.Value);
    }

    private float ResolveRandomWaitAtPointTime()
    {
        float min = MinWaitAtPointTime != null ? MinWaitAtPointTime.Value : 0.1f;
        float max = MaxWaitAtPointTime != null ? MaxWaitAtPointTime.Value : 0.3f;

        min = Mathf.Max(0f, min);
        max = Mathf.Max(0f, max);

        if (max < min)
            max = min;

        return UnityEngine.Random.Range(min, max);
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

    private bool ResolveResetCenterAfterReach()
    {
        if (ResetCenterAfterReach == null)
            return true;

        return ResetCenterAfterReach.Value;
    }

    private bool ResolveForceZeroVelocityAtPoint()
    {
        if (ForceZeroVelocityAtPoint == null)
            return true;

        return ForceZeroVelocityAtPoint.Value;
    }

    private bool ResolveReturnRunningWhileActive()
    {
        if (ReturnRunningWhileActive == null)
            return false;

        return ReturnRunningWhileActive.Value;
    }
}