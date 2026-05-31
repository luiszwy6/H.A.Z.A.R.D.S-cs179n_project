using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using UnityEngine.AI;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Patrol Around Transform",
    story: "[Self] patrols around [PatrolCenter] using [MoveMode]",
    category: "Enemy/Movement",
    id: "a3c1e7f2b4d64a8c9e0f1b2c3d4e5f6a"
)]
public partial class PatrolAroundTransformAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> PatrolCenter;
    [SerializeReference] public BlackboardVariable<float> PatrolRadius;
    [SerializeReference] public BlackboardVariable<float> ReachDistance;
    [SerializeReference] public BlackboardVariable<float> PatrolPointStopTime;
    [SerializeReference] public BlackboardVariable<float> RotationSpeed;
    [SerializeReference] public BlackboardVariable<EnemyMoveMode> MoveMode;

    [SerializeReference] public BlackboardVariable<bool> KeepAimingDuringPatrol;
    [SerializeReference] public BlackboardVariable<bool> LookAtPatrolPoint;

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
        if (Self?.Value == null)
            return Status.Failure;

        agent = Self.Value.GetComponent<NavMeshAgent>();
        animatorDriver = Self.Value.GetComponent<EnemyAnimatorParameterDriver>();
        sensor = Self.Value.GetComponent<EnemySensor>();

        if (agent == null)
            return Status.Failure;

        if (!TryResolveCenter(out patrolCenter))
            return Status.Failure;

        EnemyMoveMode moveMode = MoveMode != null ? MoveMode.Value : EnemyMoveMode.Walk;
        bool keepAiming = KeepAimingDuringPatrol != null && KeepAimingDuringPatrol.Value;
        bool lookAtPoint = LookAtPatrolPoint == null || LookAtPatrolPoint.Value;

        if (animatorDriver != null)
        {
            animatorDriver.SetMoveMode(moveMode);
            if (ClearCombatOnStart) animatorDriver.ClearCombat();
            animatorDriver.SetAiming(keepAiming);
        }

        agent.isStopped = false;
        agent.updateRotation = !lookAtPoint;

        hasDestination = false;
        isWaitingAtPoint = false;
        waitTimer = 0f;
        currentPatrolPoint = Self.Value.transform.position;

        return PickNewPatrolPoint() ? Status.Running : Status.Failure;
    }

    protected override Status OnUpdate()
    {
        if (agent == null || Self?.Value == null || !agent.enabled || !agent.isOnNavMesh)
            return Status.Failure;

        if (InterruptWhenTargetSeen && sensor != null)
        {
            sensor.RefreshSensor();
            if (sensor.CanSeeTarget)
                return Status.Failure;
        }

        if (LookAtPatrolPoint != null && LookAtPatrolPoint.Value)
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

        if (!hasDestination && !PickNewPatrolPoint())
            return Status.Failure;

        float reach = ReachDistance != null ? ReachDistance.Value : 1f;

        if (!agent.pathPending && agent.remainingDistance <= reach)
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
        if (agent == null) return false;

        // Refresh center in case the transform moved
        TryResolveCenter(out patrolCenter);

        float radius = PatrolRadius != null ? PatrolRadius.Value : 6f;
        bool lookAtPoint = LookAtPatrolPoint == null || LookAtPatrolPoint.Value;

        for (int i = 0; i < SampleAttempts; i++)
        {
            Vector2 circle = UnityEngine.Random.insideUnitCircle * radius;
            Vector3 candidate = patrolCenter + new Vector3(circle.x, 0f, circle.y);

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                currentPatrolPoint = hit.position;
                agent.isStopped = false;
                agent.updateRotation = !lookAtPoint;
                agent.SetDestination(currentPatrolPoint);
                hasDestination = true;
                return true;
            }
        }

        hasDestination = false;
        return false;
    }

    private bool TryResolveCenter(out Vector3 center)
    {
        if (PatrolCenter?.Value != null)
        {
            center = PatrolCenter.Value.transform.position;
            return true;
        }

        if (Self?.Value != null)
        {
            center = Self.Value.transform.position;
            return true;
        }

        center = Vector3.zero;
        return false;
    }

    private void FacePatrolPoint()
    {
        if (Self?.Value == null) return;

        Vector3 dir = currentPatrolPoint - Self.Value.transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.01f) return;

        float speed = RotationSpeed != null ? RotationSpeed.Value : 10f;
        Self.Value.transform.rotation = Quaternion.Slerp(
            Self.Value.transform.rotation,
            Quaternion.LookRotation(dir.normalized),
            speed * Time.deltaTime
        );
    }
}
