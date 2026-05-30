using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using UnityEngine.AI;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Stop At Self Tactical Point For Duration",
    story: "[Self] stops at self tactical point [TacticalMovePoint] for [Duration] seconds",
    category: "Enemy/Movement",
    id: "e77f0a4de2c24f2ba4ec47c1747f9c28"
)]
public partial class StopAtSelfTacticalPointForDurationAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<Transform> TacticalMovePoint;
    [SerializeReference] public BlackboardVariable<bool> HasTacticalMovePoint;
    [SerializeReference] public BlackboardVariable<float> Duration;

    [SerializeReference] public BlackboardVariable<bool> ClearCombatOnStart;
    [SerializeReference] public BlackboardVariable<bool> ResetPathOnStart;
    [SerializeReference] public BlackboardVariable<bool> ForceZeroVelocityOnStart;
    [SerializeReference] public BlackboardVariable<bool> DisableAgentRotationOnStart;
    [SerializeReference] public BlackboardVariable<bool> EnableAgentRotationOnEnd;
    [SerializeReference] public BlackboardVariable<bool> ResumeAgentOnEnd;
    [SerializeReference] public BlackboardVariable<EnemyMoveMode> MoveMode;

    private float timer;
    private NavMeshAgent agent;
    private EnemyAnimatorParameterDriver animatorDriver;

    protected override Status OnStart()
    {
        if (Self == null || Self.Value == null)
            return Status.Failure;

        timer = 0f;
        agent = Self.Value.GetComponent<NavMeshAgent>();
        animatorDriver = Self.Value.GetComponent<EnemyAnimatorParameterDriver>();

        SetTacticalMovePointToSelf();

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;

            if (ResolveResetPathOnStart())
                agent.ResetPath();

            if (ResolveForceZeroVelocityOnStart())
                agent.velocity = Vector3.zero;

            if (ResolveDisableAgentRotationOnStart())
                agent.updateRotation = false;
        }

        if (animatorDriver != null)
        {
            EnemyMoveMode moveMode = ResolveMoveMode();

            if (moveMode != EnemyMoveMode.None)
                animatorDriver.SetMoveMode(moveMode);

            if (ResolveClearCombatOnStart())
                animatorDriver.ClearCombat();
        }

        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (Self == null || Self.Value == null)
            return Status.Failure;

        SetTacticalMovePointToSelf();

        timer += Time.deltaTime;

        float duration = Duration != null ? Mathf.Max(0f, Duration.Value) : 1f;
        return timer >= duration ? Status.Success : Status.Running;
    }

    protected override void OnEnd()
    {
        timer = 0f;

        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        if (ResolveEnableAgentRotationOnEnd())
            agent.updateRotation = true;

        if (ResolveResumeAgentOnEnd())
            agent.isStopped = false;
    }

    private void SetTacticalMovePointToSelf()
    {
        if (Self == null || Self.Value == null)
            return;

        if (TacticalMovePoint != null && TacticalMovePoint.Value != null)
            TacticalMovePoint.Value.position = Self.Value.transform.position;

        if (HasTacticalMovePoint != null)
            HasTacticalMovePoint.Value = true;
    }

    private bool ResolveClearCombatOnStart()
    {
        return ClearCombatOnStart == null || ClearCombatOnStart.Value;
    }

    private bool ResolveResetPathOnStart()
    {
        return ResetPathOnStart == null || ResetPathOnStart.Value;
    }

    private bool ResolveForceZeroVelocityOnStart()
    {
        return ForceZeroVelocityOnStart == null || ForceZeroVelocityOnStart.Value;
    }

    private bool ResolveDisableAgentRotationOnStart()
    {
        return DisableAgentRotationOnStart != null && DisableAgentRotationOnStart.Value;
    }

    private bool ResolveEnableAgentRotationOnEnd()
    {
        return EnableAgentRotationOnEnd != null && EnableAgentRotationOnEnd.Value;
    }

    private bool ResolveResumeAgentOnEnd()
    {
        return ResumeAgentOnEnd == null || ResumeAgentOnEnd.Value;
    }

    private EnemyMoveMode ResolveMoveMode()
    {
        if (MoveMode == null)
            return EnemyMoveMode.None;

        return MoveMode.Value;
    }
}
