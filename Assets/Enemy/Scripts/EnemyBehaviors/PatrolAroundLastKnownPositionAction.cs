using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using UnityEngine.AI;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Patrol Around Last Known Position",
    story: "[Self] patrols around last known position using [MoveMode]",
    category: "Enemy/Movement",
    id: "b22a6a9478dd4f778f67a1f3fb8b2fe4"
)]
public partial class PatrolAroundLastKnownPositionAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<Vector3> LastKnownPosition;
    [SerializeReference] public BlackboardVariable<float> PatrolRadius;
    [SerializeReference] public BlackboardVariable<float> ReachDistance;
    [SerializeReference] public BlackboardVariable<float> PatrolPointStopTime;
    [SerializeReference] public BlackboardVariable<float> RotationSpeed;
    [SerializeReference] public BlackboardVariable<EnemyMoveMode> MoveMode;

    [SerializeReference] public BlackboardVariable<bool> KeepAimingDuringPatrol;
    [SerializeReference] public BlackboardVariable<bool> LookAtPatrolPoint;

    [SerializeField] public bool PreferLastKnownMarker = true;
    [SerializeField] public bool ClearCombatOnStart = true;
    [SerializeField] public bool RestoreAgentRotationOnEnd = true;
    [SerializeField] public bool ClearAimingOnEnd = true;
    [SerializeField] public bool InterruptWhenTargetSeen = true;
    [SerializeField] public int SampleAttempts = 12;

    private NavMeshAgent agent;
    private EnemyAnimatorParameterDriver animatorDriver;
    private EnemySensor sensor;

    private bool hasDestination;
    private bool isWaitingAtPoint;
    private float waitTimer;

    private Vector3 patrolCenter;
    private Vector3 currentPatrolPoint;

    protected override Status OnStart()
    {
        if (Self == null || Self.Value == null)
            return Status.Failure;

        agent = Self.Value.GetComponent<NavMeshAgent>();
        animatorDriver = Self.Value.GetComponent<EnemyAnimatorParameterDriver>();
        sensor = Self.Value.GetComponent<EnemySensor>();

        if (agent == null)
            return Status.Failure;

        if (sensor != null)
            sensor.RefreshSensor();

        if (!TryResolvePatrolCenter(out patrolCenter))
            return Status.Failure;

        EnemyMoveMode moveMode = ResolveMoveMode();
        bool keepAiming = ResolveKeepAimingDuringPatrol();
        bool lookAtPoint = ResolveLookAtPatrolPoint();

        if (animatorDriver != null)
        {
            animatorDriver.SetMoveMode(moveMode);

            if (ClearCombatOnStart)
                animatorDriver.ClearCombat();

            animatorDriver.SetAiming(keepAiming);
        }

        agent.isStopped = false;
        agent.updateRotation = !lookAtPoint;

        hasDestination = false;
        isWaitingAtPoint = false;
        waitTimer = 0f;
        currentPatrolPoint = Self.Value.transform.position;

        if (!PickNewPatrolPoint())
            return Status.Failure;

        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (agent == null)
            return Status.Failure;

        if (Self == null || Self.Value == null)
            return Status.Failure;

        if (InterruptWhenTargetSeen && sensor != null)
        {
            sensor.RefreshSensor();

            if (sensor.CanSeeTarget)
                return Status.Failure;
        }

        if (ResolveLookAtPatrolPoint())
            FacePatrolPoint();

        if (isWaitingAtPoint)
        {
            waitTimer += Time.deltaTime;

            float stopTime = PatrolPointStopTime != null ? PatrolPointStopTime.Value : 1.5f;

            if (waitTimer >= stopTime)
            {
                isWaitingAtPoint = false;
                waitTimer = 0f;

                if (!PickNewPatrolPoint())
                    return Status.Failure;
            }

            return Status.Running;
        }

        if (!hasDestination)
        {
            if (!PickNewPatrolPoint())
                return Status.Failure;
        }

        float reachDistance = ReachDistance != null ? ReachDistance.Value : 1.0f;

        if (!agent.pathPending && agent.remainingDistance <= reachDistance)
        {
            agent.isStopped = true;
            agent.ResetPath();

            hasDestination = false;
            isWaitingAtPoint = true;
            waitTimer = 0f;
        }

        return Status.Running;
    }

    protected override void OnEnd()
    {
        isWaitingAtPoint = false;
        waitTimer = 0f;
        hasDestination = false;

        if (agent != null && RestoreAgentRotationOnEnd)
            agent.updateRotation = true;

        if (animatorDriver != null && ClearAimingOnEnd)
            animatorDriver.SetAiming(false);
    }

    private bool PickNewPatrolPoint()
    {
        if (agent == null)
            return false;

        float radius = PatrolRadius != null ? PatrolRadius.Value : 6f;

        for (int i = 0; i < SampleAttempts; i++)
        {
            Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * radius;
            Vector3 candidate = patrolCenter + new Vector3(randomCircle.x, 0f, randomCircle.y);

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
            {
                currentPatrolPoint = hit.position;

                agent.isStopped = false;
                agent.updateRotation = !ResolveLookAtPatrolPoint();
                agent.SetDestination(currentPatrolPoint);

                hasDestination = true;
                return true;
            }
        }

        hasDestination = false;
        return false;
    }

    private void FacePatrolPoint()
    {
        if (Self == null || Self.Value == null)
            return;

        Transform selfTransform = Self.Value.transform;

        Vector3 direction = currentPatrolPoint - selfTransform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f)
            return;

        float rotationSpeed = RotationSpeed != null ? RotationSpeed.Value : 10f;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);

        selfTransform.rotation = Quaternion.Slerp(
            selfTransform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }

    private bool TryResolvePatrolCenter(out Vector3 center)
    {
        center = Vector3.zero;

        if (PreferLastKnownMarker &&
            sensor != null &&
            sensor.HasLastKnownPosition &&
            sensor.LastKnownPositionMarker != null)
        {
            center = sensor.LastKnownPositionMarker.position;
            return true;
        }

        if (sensor != null && sensor.HasLastKnownPosition)
        {
            center = sensor.LastKnownPosition;
            return true;
        }

        if (LastKnownPosition != null)
        {
            center = LastKnownPosition.Value;
            return true;
        }

        if (Self != null && Self.Value != null)
        {
            center = Self.Value.transform.position;
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

    private bool ResolveKeepAimingDuringPatrol()
    {
        if (KeepAimingDuringPatrol == null)
            return false;

        return KeepAimingDuringPatrol.Value;
    }

    private bool ResolveLookAtPatrolPoint()
    {
        if (LookAtPatrolPoint == null)
            return true;

        return LookAtPatrolPoint.Value;
    }
}