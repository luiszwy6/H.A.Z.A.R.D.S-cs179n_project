using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using UnityEngine.AI;
using Action = Unity.Behavior.Action;

public enum FaceTargetCompletionMode
{
    Running,
    SuccessEveryTick,
    SuccessWhenFacingTarget,
    SuccessWhenTacticalMovePointReached
}

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Face Target Continuously",
    story: "[Self] continuously faces [Target]",
    category: "Enemy/Movement",
    id: "c43051d9fc9f4d20967d32e704aa62fb"
)]
public partial class FaceTargetContinuouslyAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> Target;
    [SerializeReference] public BlackboardVariable<Transform> TacticalMovePoint;
    [SerializeReference] public BlackboardVariable<float> RotationSpeed;
    [SerializeReference] public BlackboardVariable<float> StopDistance;
    [SerializeReference] public BlackboardVariable<bool> RestoreAgentRotationOnEnd;
    [SerializeReference] public BlackboardVariable<bool> UsePlayerStatusIfTargetMissing;
    [SerializeReference] public BlackboardVariable<bool> SucceedWhenTacticalMovePointReached;
    [SerializeReference] public BlackboardVariable<FaceTargetCompletionMode> CompletionMode;
    [SerializeReference] public BlackboardVariable<float> AngleTolerance;

    private NavMeshAgent agent;

    protected override Status OnStart()
    {
        if (Self == null || Self.Value == null)
            return Status.Failure;

        if (!HasValidTarget())
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

        Transform targetTransform = ResolveTargetTransform();

        if (targetTransform == null)
            return Status.Failure;

        if (agent != null)
            agent.updateRotation = false;

        Transform selfTransform = Self.Value.transform;
        Vector3 direction = targetTransform.position - selfTransform.position;
        direction.y = 0f;

        bool isFacingTarget = true;

        if (direction.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);

            selfTransform.rotation = Quaternion.Slerp(
                selfTransform.rotation,
                targetRotation,
                ResolveRotationSpeed() * Time.deltaTime
            );

            float angle = Vector3.Angle(selfTransform.forward, direction.normalized);
            isFacingTarget = angle <= ResolveAngleTolerance();
        }

        switch (ResolveCompletionMode())
        {
            case FaceTargetCompletionMode.SuccessEveryTick:
                return Status.Success;

            case FaceTargetCompletionMode.SuccessWhenFacingTarget:
                return isFacingTarget ? Status.Success : Status.Running;

            case FaceTargetCompletionMode.SuccessWhenTacticalMovePointReached:
                return HasReachedTacticalMovePoint() ? Status.Success : Status.Running;

            case FaceTargetCompletionMode.Running:
            default:
                return Status.Running;
        }
    }

    protected override void OnEnd()
    {
        if (agent != null && ResolveRestoreAgentRotationOnEnd())
            agent.updateRotation = true;
    }

    private bool HasValidTarget()
    {
        return ResolveTargetTransform() != null;
    }

    private Transform ResolveTargetTransform()
    {
        if (Target != null && Target.Value != null)
            return Target.Value.transform;

        if (!ResolveUsePlayerStatusIfTargetMissing())
            return null;

        PlayerStatus playerStatus = PlayerStatus.Instance;

        return playerStatus != null ? playerStatus.transform : null;
    }

    private float ResolveRotationSpeed()
    {
        if (RotationSpeed == null)
            return 12f;

        return Mathf.Max(0f, RotationSpeed.Value);
    }

    private float ResolveAngleTolerance()
    {
        if (AngleTolerance == null)
            return 3f;

        return Mathf.Max(0f, AngleTolerance.Value);
    }

    private bool ResolveRestoreAgentRotationOnEnd()
    {
        if (RestoreAgentRotationOnEnd == null)
            return false;

        return RestoreAgentRotationOnEnd.Value;
    }

    private bool ResolveUsePlayerStatusIfTargetMissing()
    {
        if (UsePlayerStatusIfTargetMissing == null)
            return true;

        return UsePlayerStatusIfTargetMissing.Value;
    }

    private bool ResolveSucceedWhenTacticalMovePointReached()
    {
        if (SucceedWhenTacticalMovePointReached == null)
            return false;

        return SucceedWhenTacticalMovePointReached.Value;
    }

    private FaceTargetCompletionMode ResolveCompletionMode()
    {
        if (CompletionMode != null)
            return CompletionMode.Value;

        if (ResolveSucceedWhenTacticalMovePointReached())
            return FaceTargetCompletionMode.SuccessWhenTacticalMovePointReached;

        return FaceTargetCompletionMode.SuccessEveryTick;
    }

    private bool HasReachedTacticalMovePoint()
    {
        if (Self == null || Self.Value == null)
            return false;

        if (TacticalMovePoint == null || TacticalMovePoint.Value == null)
            return false;

        float stopDistance = StopDistance != null
            ? Mathf.Max(0f, StopDistance.Value)
            : 0.75f;

        return Vector3.Distance(
            Self.Value.transform.position,
            TacticalMovePoint.Value.position
        ) <= stopDistance;
    }
}
