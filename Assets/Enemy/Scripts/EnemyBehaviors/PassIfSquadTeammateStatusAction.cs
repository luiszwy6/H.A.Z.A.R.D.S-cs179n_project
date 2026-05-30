using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

public enum SquadTeammateStatusCheck
{
    IsFlanking,
    CanSeeTarget,
    IsReloading
}

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Pass If Squad Teammate Status",
    story: "[Self] passes if teammate type [EnemyType] has status [StatusCheck]",
    category: "Enemy/Squad",
    id: "f7c52b7c4c87424f9a81dd69110b13e5"
)]
public partial class PassIfSquadTeammateStatusAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<SquadEnemyType> EnemyType;
    [SerializeReference] public BlackboardVariable<SquadTeammateStatusCheck> StatusCheck;
    [SerializeReference] public BlackboardVariable<bool> InvertResult;

    protected override Status OnUpdate()
    {
        if (Self == null || Self.Value == null)
            return Status.Failure;

        SquadMember squadMember = Self.Value.GetComponent<SquadMember>();

        if (squadMember == null || squadMember.SquadManager == null)
            return Status.Failure;

        SquadEnemyType enemyType = ResolveEnemyType();
        SquadTeammateStatusCheck statusCheck = ResolveStatusCheck();

        bool result = CheckStatus(squadMember, enemyType, statusCheck);

        if (ResolveInvertResult())
            result = !result;

        return result ? Status.Success : Status.Failure;
    }

    private bool CheckStatus(
        SquadMember squadMember,
        SquadEnemyType enemyType,
        SquadTeammateStatusCheck statusCheck)
    {
        SquadManager squadManager = squadMember.SquadManager;

        switch (statusCheck)
        {
            case SquadTeammateStatusCheck.CanSeeTarget:
                return squadManager.HasTeammateSeeingTarget(squadMember, enemyType);

            case SquadTeammateStatusCheck.IsReloading:
                return squadManager.HasTeammateReloading(squadMember, enemyType);

            case SquadTeammateStatusCheck.IsFlanking:
            default:
                return squadManager.HasTeammateFlanking(squadMember, enemyType);
        }
    }

    private SquadEnemyType ResolveEnemyType()
    {
        if (EnemyType == null)
            return SquadEnemyType.AR;

        return EnemyType.Value;
    }

    private SquadTeammateStatusCheck ResolveStatusCheck()
    {
        if (StatusCheck == null)
            return SquadTeammateStatusCheck.IsFlanking;

        return StatusCheck.Value;
    }

    private bool ResolveInvertResult()
    {
        if (InvertResult == null)
            return false;

        return InvertResult.Value;
    }
}
