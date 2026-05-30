using System;
using System.Collections.Generic;
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
    private static readonly Dictionary<int, Vector3> activeStrafeDestinations =
        new Dictionary<int, Vector3>();

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
    [SerializeReference] public BlackboardVariable<bool> AvoidTeammateStrafePoints;
    [SerializeReference] public BlackboardVariable<float> TeammateAvoidDistance;
    [SerializeReference] public BlackboardVariable<int> MaxPickAttempts;
    [SerializeReference] public BlackboardVariable<bool> StopWhileStunned;
    [SerializeReference] public BlackboardVariable<bool> StopWhileFlashBangStunned;

    private NavMeshAgent agent;
    private EnemyAnimatorParameterDriver animatorDriver;
    private EnemyStatus enemyStatus;
    private EnemyStunReceiver stunReceiver;
    private SquadMember squadMember;
    private NavMeshPath path;

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
            ClearRegisteredDestination();
            cachedSelf = Self.Value;
            ResetRuntimeState();
        }

        agent = Self.Value.GetComponent<NavMeshAgent>();
        animatorDriver = Self.Value.GetComponent<EnemyAnimatorParameterDriver>();
        enemyStatus = Self.Value.GetComponent<EnemyStatus>();
        stunReceiver = Self.Value.GetComponent<EnemyStunReceiver>();
        squadMember = Self.Value.GetComponent<SquadMember>();
        path = new NavMeshPath();

        if (agent == null)
            return Status.Failure;

        if (!agent.isOnNavMesh)
            return Status.Failure;

        if (ShouldStopForStun())
            return StopStrafeForStun();

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

        if (ShouldStopForStun())
            return StopStrafeForStun();

        if (animatorDriver != null)
            animatorDriver.SetMoveMode(ResolveMoveMode());

        if (ResolveDisableAgentRotation())
            agent.updateRotation = false;

        return TickStrafe();
    }

    protected override void OnEnd()
    {
        ClearRegisteredDestination();
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

        int attempts = ResolveMaxPickAttempts();
        float sampleDistance = ResolveNavMeshSampleDistance();

        for (int i = 0; i < attempts; i++)
        {
            Vector3 candidate;

            if (ResolveStrafePattern() == EnemyStrafeMovePattern.LeftRightFromFacing)
                candidate = GetLeftRightCandidate(i);
            else
                candidate = GetRandomPointAroundCenterCandidate();

            if (!NavMesh.SamplePosition(
                    candidate,
                    out NavMeshHit hit,
                    sampleDistance,
                    agent.areaMask))
            {
                continue;
            }

            if (!IsReachable(hit.position))
                continue;

            if (IsTooCloseToTeammateStrafePoint(hit.position))
                continue;

            currentDestination = hit.position;
            RegisterDestination(currentDestination);

            agent.isStopped = false;

            if (ResolveDisableAgentRotation())
                agent.updateRotation = false;

            agent.SetDestination(currentDestination);
            hasDestination = true;

            return true;
        }

        hasDestination = false;
        ClearRegisteredDestination();
        return false;
    }

    private Vector3 GetLeftRightCandidate(int attempt)
    {
        Transform selfTransform = Self.Value.transform;

        float min = ResolveMinStrafeDistance();
        float max = ResolveMaxStrafeDistance();

        if (max < min)
            max = min;

        float distance = UnityEngine.Random.Range(min, max);
        float side;

        if (attempt == 0)
            side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        else
            side = attempt % 2 == 0 ? 1f : -1f;

        Vector3 right = selfTransform.right;
        right.y = 0f;

        if (right.sqrMagnitude < 0.01f)
            right = Vector3.right;

        Vector3 forward = selfTransform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.01f)
            forward = Vector3.forward;

        float forwardOffset = attempt <= 1
            ? 0f
            : UnityEngine.Random.Range(-0.5f, 0.5f) * distance;

        return selfTransform.position +
               right.normalized * side * distance +
               forward.normalized * forwardOffset;
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

    private bool ResolveAvoidTeammateStrafePoints()
    {
        if (AvoidTeammateStrafePoints == null)
            return true;

        return AvoidTeammateStrafePoints.Value;
    }

    private float ResolveTeammateAvoidDistance()
    {
        if (TeammateAvoidDistance == null)
            return 1.5f;

        return Mathf.Max(0f, TeammateAvoidDistance.Value);
    }

    private int ResolveMaxPickAttempts()
    {
        if (MaxPickAttempts == null)
            return 8;

        return Mathf.Max(1, MaxPickAttempts.Value);
    }

    private bool IsReachable(Vector3 destination)
    {
        if (agent == null || !agent.isOnNavMesh)
            return false;

        if (path == null)
            path = new NavMeshPath();

        if (!agent.CalculatePath(destination, path))
            return false;

        return path.status == NavMeshPathStatus.PathComplete;
    }

    private bool IsTooCloseToTeammateStrafePoint(Vector3 destination)
    {
        if (!ResolveAvoidTeammateStrafePoints())
            return false;

        float avoidDistance = ResolveTeammateAvoidDistance();

        if (avoidDistance <= 0f)
            return false;

        if (squadMember == null)
            squadMember = Self.Value.GetComponent<SquadMember>();

        if (squadMember == null || squadMember.SquadManager == null)
            return false;

        float avoidDistanceSqr = avoidDistance * avoidDistance;
        var members = squadMember.SquadManager.Members;

        for (int i = 0; i < members.Count; i++)
        {
            SquadMember member = members[i];

            if (member == null || member == squadMember || !member.IsAlive)
                continue;

            int teammateKey = member.gameObject.GetInstanceID();

            if (activeStrafeDestinations.TryGetValue(teammateKey, out Vector3 teammateDestination))
            {
                if ((destination - teammateDestination).sqrMagnitude < avoidDistanceSqr)
                    return true;
            }

            if ((destination - member.transform.position).sqrMagnitude < avoidDistanceSqr)
                return true;
        }

        return false;
    }

    private void RegisterDestination(Vector3 destination)
    {
        if (Self == null || Self.Value == null)
            return;

        activeStrafeDestinations[Self.Value.GetInstanceID()] = destination;
    }

    private Status StopStrafeForStun()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }

        ClearRegisteredDestination();
        ResetRuntimeState();
        return Status.Failure;
    }

    private bool ShouldStopForStun()
    {
        if (Self == null || Self.Value == null)
            return false;

        if (ResolveStopWhileStunned())
        {
            if (stunReceiver == null)
                stunReceiver = Self.Value.GetComponent<EnemyStunReceiver>();

            if (stunReceiver != null && stunReceiver.IsStunned)
                return true;
        }

        if (ResolveStopWhileFlashBangStunned())
        {
            if (enemyStatus == null)
                enemyStatus = Self.Value.GetComponent<EnemyStatus>();

            if (enemyStatus != null && enemyStatus.IsFlashBangStun)
                return true;
        }

        return false;
    }

    private bool ResolveStopWhileStunned()
    {
        if (StopWhileStunned == null)
            return true;

        return StopWhileStunned.Value;
    }

    private bool ResolveStopWhileFlashBangStunned()
    {
        if (StopWhileFlashBangStunned == null)
            return true;

        return StopWhileFlashBangStunned.Value;
    }

    private void ClearRegisteredDestination()
    {
        if (cachedSelf != null)
            activeStrafeDestinations.Remove(cachedSelf.GetInstanceID());

        if (Self != null && Self.Value != null)
            activeStrafeDestinations.Remove(Self.Value.GetInstanceID());
    }
}
