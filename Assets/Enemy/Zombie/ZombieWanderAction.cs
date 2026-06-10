using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using UnityEngine.AI;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Zombie Wander",
    story: "[Self] wanders randomly in radius [WanderRadius]",
    category: "Zombie",
    id: "b3f1a2c4d5e6f7a8b9c0d1e2f3a4b5c6"
)]
public partial class ZombieWanderAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<float> WanderRadius;
    [SerializeReference] public BlackboardVariable<bool> IsInRangeOfPlayer;

    [Header("Wait")]
    [SerializeField] private float minWaitTime = 0.5f;
    [SerializeField] private float maxWaitTime = 2f;

    [Header("Movement")]
    [SerializeField] private float stopDistance = 0.5f;
    [SerializeField] private int maxPickAttempts = 8;
    [SerializeField] private float navMeshSampleRadius = 2f;

    private enum WanderState { PickingPoint, Moving, Waiting }

    private NavMeshAgent agent;
    private WanderState state;
    private float waitEndTime;
    private Vector3 wanderCenter;

    protected override Status OnStart()
    {
        if (Self == null || Self.Value == null)
            return Status.Failure;

        agent = Self.Value.GetComponent<NavMeshAgent>();

        if (agent == null)
            return Status.Failure;

        wanderCenter = Self.Value.transform.position;
        state = WanderState.PickingPoint;
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (IsInRangeOfPlayer != null && IsInRangeOfPlayer.Value)
            return Status.Failure;

        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return Status.Failure;

        switch (state)
        {
            case WanderState.PickingPoint:
                return TickPickingPoint();

            case WanderState.Moving:
                return TickMoving();

            case WanderState.Waiting:
                return TickWaiting();
        }

        return Status.Running;
    }

    protected override void OnEnd()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity  = Vector3.zero;
            agent.ResetPath();
        }
    }

    private Status TickPickingPoint()
    {
        float radius = WanderRadius != null ? Mathf.Max(0.5f, WanderRadius.Value) : 5f;

        for (int i = 0; i < maxPickAttempts; i++)
        {
            Vector2 circle = UnityEngine.Random.insideUnitCircle * radius;
            Vector3 candidate = wanderCenter + new Vector3(circle.x, 0f, circle.y);

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSampleRadius, agent.areaMask))
                continue;

            agent.isStopped = false;
            agent.SetDestination(hit.position);
            state = WanderState.Moving;
            return Status.Running;
        }

        // Couldn't find a point — wait briefly and try again
        waitEndTime = Time.time + 0.5f;
        state = WanderState.Waiting;
        return Status.Running;
    }

    private Status TickMoving()
    {
        if (agent.pathPending)
            return Status.Running;

        float distance = Vector3.Distance(
            Self.Value.transform.position,
            agent.destination
        );

        if (distance <= stopDistance || (!agent.hasPath && !agent.pathPending))
        {
            agent.isStopped = true;
            agent.velocity  = Vector3.zero;
            agent.ResetPath();

            float wait = UnityEngine.Random.Range(minWaitTime, maxWaitTime);
            waitEndTime = Time.time + wait;
            state = WanderState.Waiting;
        }

        return Status.Running;
    }

    private Status TickWaiting()
    {
        if (Time.time >= waitEndTime)
            return Status.Success;

        return Status.Running;
    }
}
