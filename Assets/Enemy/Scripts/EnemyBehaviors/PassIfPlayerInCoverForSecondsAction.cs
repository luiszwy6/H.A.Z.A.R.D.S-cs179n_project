using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Pass If Player In Cover For Seconds",
    story: "passes if player is in cover for [RequiredDuration] seconds",
    category: "Enemy/Conditions",
    id: "b19c42f91e4f43b0a52f3b6b2850d9aa"
)]
public partial class PassIfPlayerInCoverForSecondsAction : Action
{
    [SerializeReference] public BlackboardVariable<bool> IsPlayerInCover;
    [SerializeReference] public BlackboardVariable<float> RequiredDuration;
    [SerializeReference] public BlackboardVariable<bool> ReturnRunningWhileWaiting;

    private float coverTimer;

    protected override Status OnStart()
    {
        coverTimer = 0f;
        return TickCondition();
    }

    protected override Status OnUpdate()
    {
        return TickCondition();
    }

    protected override void OnEnd()
    {
        coverTimer = 0f;
    }

    private Status TickCondition()
    {
        if (!ResolveIsPlayerInCover())
        {
            coverTimer = 0f;
            return Status.Failure;
        }

        coverTimer += Time.deltaTime;

        if (coverTimer >= ResolveRequiredDuration())
            return Status.Success;

        return ResolveReturnRunningWhileWaiting()
            ? Status.Running
            : Status.Failure;
    }

    private bool ResolveIsPlayerInCover()
    {
        if (IsPlayerInCover != null)
            return IsPlayerInCover.Value;

        PlayerStatus playerStatus = PlayerStatus.Instance;

        return playerStatus != null && playerStatus.IsInCover;
    }

    private float ResolveRequiredDuration()
    {
        if (RequiredDuration == null)
            return 6f;

        return Mathf.Max(0f, RequiredDuration.Value);
    }

    private bool ResolveReturnRunningWhileWaiting()
    {
        if (ReturnRunningWhileWaiting == null)
            return true;

        return ReturnRunningWhileWaiting.Value;
    }
}
