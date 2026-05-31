using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using UnityEngine.AI;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Stop Moving",
    story: "[Self] stops moving",
    category: "Enemy/Movement",
    id: "6a3d719c7e7f44cbb7460a59ec8f8c12"
)]
public partial class StopMovingAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;

    [SerializeReference] public BlackboardVariable<bool> SetMoveModeOnStop;
    [SerializeReference] public BlackboardVariable<EnemyMoveMode> StopMoveMode;
    [SerializeReference] public BlackboardVariable<bool> ForceZeroVelocity;
    [SerializeReference] public BlackboardVariable<bool> RestoreAgentRotation;

    protected override Status OnUpdate()
    {
        if (Self == null || Self.Value == null)
            return Status.Failure;

        NavMeshAgent agent = Self.Value.GetComponent<NavMeshAgent>();

        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return Status.Failure;

        EnemyAnimatorParameterDriver animatorDriver =
            Self.Value.GetComponent<EnemyAnimatorParameterDriver>();

        bool setMoveModeOnStop = ResolveSetMoveModeOnStop();
        EnemyMoveMode stopMoveMode = ResolveStopMoveMode();
        bool forceZeroVelocity = ResolveForceZeroVelocity();
        bool restoreAgentRotation = ResolveRestoreAgentRotation();

        if (animatorDriver != null && setMoveModeOnStop)
            animatorDriver.SetMoveMode(stopMoveMode);

        agent.isStopped = true;
        agent.ResetPath();

        if (forceZeroVelocity)
            agent.velocity = Vector3.zero;

        if (restoreAgentRotation)
            agent.updateRotation = true;

        return Status.Success;
    }

    private bool ResolveSetMoveModeOnStop()
    {
        if (SetMoveModeOnStop == null)
            return true;

        return SetMoveModeOnStop.Value;
    }

    private EnemyMoveMode ResolveStopMoveMode()
    {
        if (StopMoveMode == null)
            return EnemyMoveMode.Walk;

        return StopMoveMode.Value;
    }

    private bool ResolveForceZeroVelocity()
    {
        if (ForceZeroVelocity == null)
            return true;

        return ForceZeroVelocity.Value;
    }

    private bool ResolveRestoreAgentRotation()
    {
        if (RestoreAgentRotation == null)
            return true;

        return RestoreAgentRotation.Value;
    }
}