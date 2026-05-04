using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using UnityEngine.AI;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Face Target",
    story: "[Self] faces [Target]",
    category: "Enemy/Movement",
    id: "a6d2a9d6bb234a9d9fb2ce9449d80c11"
)]
public partial class FaceTargetAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> Target;
    [SerializeReference] public BlackboardVariable<float> RotationSpeed;
    [SerializeReference] public BlackboardVariable<float> AngleTolerance;
    [SerializeField] public bool RestoreAgentRotationOnEnd = false;

    private NavMeshAgent agent;

    protected override Status OnStart()
    {
        if (Self == null || Self.Value == null)
            return Status.Failure;

        if (Target == null || Target.Value == null)
            return Status.Failure;

        agent = Self.Value.GetComponent<NavMeshAgent>();

        if (agent != null)
            agent.updateRotation = false;

        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (Self == null || Self.Value == null)
            return Status.Failure;

        if (Target == null || Target.Value == null)
            return Status.Failure;

        Transform selfTransform = Self.Value.transform;
        Transform targetTransform = Target.Value.transform;

        Vector3 direction = targetTransform.position - selfTransform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f)
            return Status.Success;

        float rotationSpeed = RotationSpeed != null ? RotationSpeed.Value : 10f;
        float angleTolerance = AngleTolerance != null ? AngleTolerance.Value : 3f;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);

        selfTransform.rotation = Quaternion.Slerp(
            selfTransform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );

        float angle = Vector3.Angle(selfTransform.forward, direction.normalized);

        if (angle <= angleTolerance)
            return Status.Success;

        return Status.Running;
    }

    protected override void OnEnd()
    {
        if (agent != null && RestoreAgentRotationOnEnd)
            agent.updateRotation = true;
    }
}