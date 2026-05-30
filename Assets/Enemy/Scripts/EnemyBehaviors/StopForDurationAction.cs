using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using UnityEngine.AI;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Stop For Duration",
    story: "[Self] stops for [Duration] seconds",
    category: "Enemy/Movement",
    id: "ac2d0be63f5f438cb7ab6c0605f3cd23"
)]
public partial class StopForDurationAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<float> Duration;

    [SerializeReference] public BlackboardVariable<bool> ClearCombatOnStart;
    [SerializeReference] public BlackboardVariable<bool> RestoreAgentRotation;
    [SerializeReference] public BlackboardVariable<bool> InterruptWhenTargetSeen;
    [SerializeReference] public BlackboardVariable<EnemyMoveMode> MoveMode;

    [SerializeReference] public BlackboardVariable<bool> ResetPathOnStart;
    [SerializeReference] public BlackboardVariable<bool> ForceZeroVelocityOnStart;
    [SerializeReference] public BlackboardVariable<bool> RestoreStoppedStateOnEnd;
    [SerializeReference] public BlackboardVariable<bool> ResumeAgentOnEnd;

    private float timer;
    private NavMeshAgent agent;
    private EnemyAnimatorParameterDriver animatorDriver;
    private EnemySensor sensor;

    private bool cachedIsStopped;
    private bool hasCachedStoppedState;

    protected override Status OnStart()
    {
        if (Self == null || Self.Value == null)
            return Status.Failure;

        timer = 0f;

        agent = Self.Value.GetComponent<NavMeshAgent>();
        animatorDriver = Self.Value.GetComponent<EnemyAnimatorParameterDriver>();
        sensor = Self.Value.GetComponent<EnemySensor>();

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            cachedIsStopped = agent.isStopped;
            hasCachedStoppedState = true;

            agent.isStopped = true;

            if (ResolveResetPathOnStart())
                agent.ResetPath();

            if (ResolveForceZeroVelocityOnStart())
                agent.velocity = Vector3.zero;

            if (ResolveRestoreAgentRotation())
                agent.updateRotation = true;
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
        if (ResolveInterruptWhenTargetSeen() && sensor != null)
        {
            sensor.RefreshSensor();

            if (sensor.CanSeeTarget)
                return Status.Failure;
        }

        float duration = Duration != null ? Duration.Value : 2f;

        timer += Time.deltaTime;

        if (timer >= duration)
            return Status.Success;

        return Status.Running;
    }

    protected override void OnEnd()
    {
        timer = 0f;

        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            hasCachedStoppedState = false;
            return;
        }

        if (ResolveResumeAgentOnEnd())
        {
            agent.isStopped = false;
            hasCachedStoppedState = false;
            return;
        }

        if (ResolveRestoreStoppedStateOnEnd() && hasCachedStoppedState)
            agent.isStopped = cachedIsStopped;

        hasCachedStoppedState = false;
    }

    private bool ResolveClearCombatOnStart()
    {
        if (ClearCombatOnStart == null)
            return false;

        return ClearCombatOnStart.Value;
    }

    private bool ResolveRestoreAgentRotation()
    {
        if (RestoreAgentRotation == null)
            return true;

        return RestoreAgentRotation.Value;
    }

    private bool ResolveInterruptWhenTargetSeen()
    {
        if (InterruptWhenTargetSeen == null)
            return true;

        return InterruptWhenTargetSeen.Value;
    }

    private EnemyMoveMode ResolveMoveMode()
    {
        if (MoveMode == null)
            return EnemyMoveMode.Walk;

        return MoveMode.Value;
    }

    private bool ResolveResetPathOnStart()
    {
        if (ResetPathOnStart == null)
            return false;

        return ResetPathOnStart.Value;
    }

    private bool ResolveForceZeroVelocityOnStart()
    {
        if (ForceZeroVelocityOnStart == null)
            return true;

        return ForceZeroVelocityOnStart.Value;
    }

    private bool ResolveRestoreStoppedStateOnEnd()
    {
        if (RestoreStoppedStateOnEnd == null)
            return true;

        return RestoreStoppedStateOnEnd.Value;
    }

    private bool ResolveResumeAgentOnEnd()
    {
        if (ResumeAgentOnEnd == null)
            return true;

        return ResumeAgentOnEnd.Value;
    }
}
