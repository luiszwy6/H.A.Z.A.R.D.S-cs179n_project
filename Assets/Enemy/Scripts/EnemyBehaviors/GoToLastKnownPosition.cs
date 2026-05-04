using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using UnityEngine.AI;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Go To Last Known Position",
    story: "[Self] goes to last known position using [MoveMode]",
    category: "Enemy/Movement",
    id: "d9964df33b2f4e14808e9d8d5d48af51"
)]
public partial class GoToLastKnownPositionAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<Vector3> LastKnownPosition;
    [SerializeReference] public BlackboardVariable<float> ReachDistance;
    [SerializeReference] public BlackboardVariable<EnemyMoveMode> MoveMode;

    [SerializeField] public bool ClearCombatOnStart = true;
    [SerializeField] public bool InterruptWhenTargetSeen = true;
    [SerializeField] public bool PreferLastKnownMarker = true;

    private NavMeshAgent agent;
    private EnemyAnimatorParameterDriver animatorDriver;
    private EnemySensor sensor;

    private Vector3 destination;

    protected override Status OnStart()
    {
        if (Self == null || Self.Value == null)
            return Status.Failure;

        agent = Self.Value.GetComponent<NavMeshAgent>();
        animatorDriver = Self.Value.GetComponent<EnemyAnimatorParameterDriver>();
        sensor = Self.Value.GetComponent<EnemySensor>();

        if (agent == null)
            return Status.Failure;

        if (!TryResolveDestination(out destination))
            return Status.Failure;

        EnemyMoveMode moveMode = ResolveMoveMode();

        if (animatorDriver != null)
        {
            animatorDriver.SetMoveMode(moveMode);

            if (ClearCombatOnStart)
                animatorDriver.ClearCombat();
        }

        agent.isStopped = false;
        agent.updateRotation = true;

        if (NavMesh.SamplePosition(destination, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
            destination = hit.position;

        agent.SetDestination(destination);

        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (agent == null)
            return Status.Failure;

        if (InterruptWhenTargetSeen && sensor != null)
        {
            sensor.RefreshSensor();

            if (sensor.CanSeeTarget)
                return Status.Failure;
        }

        float reachDistance = ReachDistance != null ? ReachDistance.Value : 1.0f;

        if (!agent.pathPending && agent.remainingDistance <= reachDistance)
        {
            agent.isStopped = true;
            agent.ResetPath();
            return Status.Success;
        }

        return Status.Running;
    }

    protected override void OnEnd()
    {
    }

    private bool TryResolveDestination(out Vector3 resolvedDestination)
    {
        resolvedDestination = Vector3.zero;

        if (PreferLastKnownMarker &&
            sensor != null &&
            sensor.HasLastKnownPosition &&
            sensor.LastKnownPositionMarker != null)
        {
            resolvedDestination = sensor.LastKnownPositionMarker.position;
            return true;
        }

        if (sensor != null && sensor.HasLastKnownPosition)
        {
            resolvedDestination = sensor.LastKnownPosition;
            return true;
        }

        if (LastKnownPosition != null)
        {
            resolvedDestination = LastKnownPosition.Value;
            return true;
        }

        return false;
    }

    private EnemyMoveMode ResolveMoveMode()
    {
        if (MoveMode == null)
            return EnemyMoveMode.Run;

        return MoveMode.Value;
    }
}