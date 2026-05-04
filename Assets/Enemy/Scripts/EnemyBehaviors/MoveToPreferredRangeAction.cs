using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using UnityEngine.AI;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Move To Preferred Range",
    story: "[Self] moves toward [Target] until within preferred range using [MoveMode]",
    category: "Enemy/Movement",
    id: "f0d43d36f41b43c3a43e17128f6d71a1"
)]
public partial class MoveToPreferredRangeAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> Target;

    [SerializeReference] public BlackboardVariable<float> PreferredRange;
    [SerializeReference] public BlackboardVariable<float> StopTolerance;
    [SerializeReference] public BlackboardVariable<EnemyMoveMode> MoveMode;

    private NavMeshAgent agent;
    private EnemyAnimatorParameterDriver animatorDriver;
    private EnemySensor sensor;

    protected override Status OnStart()
    {
        if (Self == null || Self.Value == null)
            return Status.Failure;

        if (Target == null || Target.Value == null)
            return Status.Failure;

        agent = Self.Value.GetComponent<NavMeshAgent>();
        animatorDriver = Self.Value.GetComponent<EnemyAnimatorParameterDriver>();
        sensor = Self.Value.GetComponent<EnemySensor>();

        if (agent == null)
            return Status.Failure;

        EnemyMoveMode moveMode = ResolveMoveMode();

        if (animatorDriver != null)
            animatorDriver.SetMoveMode(moveMode);

        agent.isStopped = false;
        agent.updateRotation = true;

        return Status.Running;
    }

    protected override Status OnUpdate()
{
    if (agent == null)
        return Status.Failure;

    if (Self == null || Self.Value == null)
        return Status.Failure;

    if (Target == null || Target.Value == null)
        return Status.Failure;

    if (sensor != null)
    {
        sensor.RefreshSensor();

        if (!sensor.CanSeeTarget)
            return Status.Failure;
    }

    Transform selfTransform = Self.Value.transform;
    Transform targetTransform = Target.Value.transform;

    float preferredRange = PreferredRange != null ? PreferredRange.Value : 8f;
    float stopTolerance = StopTolerance != null ? StopTolerance.Value : 0.75f;

    float currentDistance = Vector3.Distance(
        selfTransform.position,
        targetTransform.position
    );

    if (currentDistance <= preferredRange + stopTolerance)
    {
        agent.isStopped = true;
        agent.ResetPath();
        return Status.Success;
    }

    agent.isStopped = false;
    agent.SetDestination(targetTransform.position);

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
}