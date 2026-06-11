using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

public enum EnemyStatusBoolCheck
{
    IsInCover,
    IsShooting,
    IsReloading,
    CanSeeTarget,
    IsFlanking,
    IsCoveringTeammate,
    IsEscapingFromGrenade,
    IsSmokeBlockingVision,
    IsFlashBangStun,
    IsFollowingShield,
    IsBackingAway,
    IsCombatStrafing,
    IsMeleeCombating,
    IsGoingToLKP,
    IsPatrol,
    Is2ndPhase
}

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Pass If Enemy Status Bool",
    story: "[Self] passes if enemy status [StatusCheck]",
    category: "Enemy/Status",
    id: "d4e2fd6a4c8f4e7db65ef6338d2b1e2f"
)]
public partial class PassIfEnemyStatusBoolAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<EnemyStatus> EnemyStatus;
    [SerializeReference] public BlackboardVariable<EnemyStatusBoolCheck> StatusCheck;
    [SerializeReference] public BlackboardVariable<bool> InvertResult;

    [SerializeReference] public BlackboardVariable<bool> ReturnRunningWhenPassing;

    [SerializeField] private bool debugLogResult = false;

    protected override Status OnUpdate()
    {
        EnemyStatus enemyStatus = ResolveEnemyStatus();

        if (enemyStatus == null)
            return Status.Failure;

        EnemyStatusBoolCheck statusCheck = ResolveStatusCheck();
        bool result = CheckStatus(enemyStatus, statusCheck);

        if (ResolveInvertResult())
            result = !result;

        if (debugLogResult)
        {
            Debug.Log(
                $"[PassIfEnemyStatusBoolAction] self={GetSelfName()}, statusOwner={enemyStatus.name}, check={statusCheck}, result={result}",
                enemyStatus
            );
        }

        if (!result)
            return Status.Failure;

        return (ReturnRunningWhenPassing != null && ReturnRunningWhenPassing.Value)
            ? Status.Running
            : Status.Success;
    }

    private EnemyStatus ResolveEnemyStatus()
    {
        if (Self != null && Self.Value != null)
        {
            EnemyStatus selfStatus = Self.Value.GetComponent<EnemyStatus>();

            if (selfStatus != null)
                return selfStatus;
        }

        if (EnemyStatus != null && EnemyStatus.Value != null)
            return EnemyStatus.Value;

        return null;
    }

    private bool CheckStatus(EnemyStatus enemyStatus, EnemyStatusBoolCheck statusCheck)
    {
        switch (statusCheck)
        {
            case EnemyStatusBoolCheck.IsInCover:
                return enemyStatus.IsInCover;

            case EnemyStatusBoolCheck.IsShooting:
                return enemyStatus.IsShooting;

            case EnemyStatusBoolCheck.IsReloading:
                return enemyStatus.IsReloading;

            case EnemyStatusBoolCheck.CanSeeTarget:
                return enemyStatus.CanSeeTarget;

            case EnemyStatusBoolCheck.IsSmokeBlockingVision:
                return enemyStatus.IsSmokeBlockingVision;

            case EnemyStatusBoolCheck.IsFlashBangStun:
                return enemyStatus.IsFlashBangStun;

            case EnemyStatusBoolCheck.IsFlanking:
                return enemyStatus.IsFlanking;

            case EnemyStatusBoolCheck.IsEscapingFromGrenade:
                return enemyStatus.IsEscapingFromGrenade;

            case EnemyStatusBoolCheck.IsFollowingShield:
                return enemyStatus.IsFollowingShield;

            case EnemyStatusBoolCheck.IsBackingAway:
                return enemyStatus.IsBackingAway;

            case EnemyStatusBoolCheck.IsCombatStrafing:
                return enemyStatus.IsCombatStrafing;

            case EnemyStatusBoolCheck.IsMeleeCombating:
                return enemyStatus.IsMeleeCombating;

            case EnemyStatusBoolCheck.IsGoingToLKP:
                return enemyStatus.IsGoingToLKP;

            case EnemyStatusBoolCheck.IsPatrol:
                return enemyStatus.IsPatrol;

            case EnemyStatusBoolCheck.Is2ndPhase:
                return enemyStatus.Is2ndPhase;

            case EnemyStatusBoolCheck.IsCoveringTeammate:
            default:
                return enemyStatus.IsCoveringTeammate;
        }
    }

    private EnemyStatusBoolCheck ResolveStatusCheck()
    {
        if (StatusCheck == null)
            return EnemyStatusBoolCheck.IsCoveringTeammate;

        return StatusCheck.Value;
    }

    private bool ResolveInvertResult()
    {
        if (InvertResult == null)
            return false;

        return InvertResult.Value;
    }

    private string GetSelfName()
    {
        if (Self == null || Self.Value == null)
            return "null";

        return Self.Value.name;
    }
}
