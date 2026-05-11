using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Update Enemy Ragdoll",
    story: "[Self] updates ragdoll data",
    category: "Enemy/State",
    id: "a0b2e4d8f3c94ad0a6b2cf4f1f0db8a4"
)]
public partial class UpdateEnemyRagdollAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;

    [SerializeReference] public BlackboardVariable<bool> IsKnockDown;
    [SerializeReference] public BlackboardVariable<bool> IsRagdolled;
    [SerializeReference] public BlackboardVariable<bool> IsDeathLocked;
    [SerializeReference] public BlackboardVariable<bool> IsGettingUp;
    [SerializeReference] public BlackboardVariable<float> GettingUpRemaining;

    private EnemyRagdollGetUp ragdollGetUp;

    protected override Status OnStart()
    {
        if (Self == null || Self.Value == null)
            return Status.Failure;

        ragdollGetUp = Self.Value.GetComponent<EnemyRagdollGetUp>();

        if (ragdollGetUp == null)
            ragdollGetUp = Self.Value.GetComponentInChildren<EnemyRagdollGetUp>(true);

        if (ragdollGetUp == null)
            return Status.Failure;

        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (ragdollGetUp == null)
            return Status.Failure;

        bool isRagdolled = ragdollGetUp.IsRagdolled;
        bool isDeathLocked = ragdollGetUp.DeathLocked;
        bool isGettingUp = ragdollGetUp.IsGettingUp;

        bool isKnockDown = isRagdolled && !isDeathLocked;

        if (IsRagdolled != null)
            IsRagdolled.Value = isRagdolled;

        if (IsDeathLocked != null)
            IsDeathLocked.Value = isDeathLocked;

        if (IsGettingUp != null)
            IsGettingUp.Value = isGettingUp;

        if (GettingUpRemaining != null)
            GettingUpRemaining.Value = ragdollGetUp.GettingUpRemaining;

        if (IsKnockDown != null)
            IsKnockDown.Value = isKnockDown;

        return Status.Success;
    }

    protected override void OnEnd()
    {
    }
}