using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Update Enemy Stun",
    story: "[Self] updates stun data",
    category: "Enemy/State",
    id: "f7a8fcb5c2354f57b8ce6c1579130f02"
)]
public partial class UpdateEnemyStunAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;

    [SerializeReference] public BlackboardVariable<bool> IsStun;
    [SerializeReference] public BlackboardVariable<float> StunRemaining;
    [SerializeReference] public BlackboardVariable<int> StunPart;

    [SerializeField] public bool DebugLog = false;

    private EnemyStunReceiver stunReceiver;

    protected override Status OnStart()
    {
        if (Self == null || Self.Value == null)
            return Status.Failure;

        stunReceiver = Self.Value.GetComponent<EnemyStunReceiver>();

        if (stunReceiver == null)
            stunReceiver = Self.Value.GetComponentInChildren<EnemyStunReceiver>(true);

        if (stunReceiver == null)
            return Status.Failure;

        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (stunReceiver == null)
            return Status.Failure;

        if (IsStun != null)
            IsStun.Value = stunReceiver.IsStunned;

        if (StunRemaining != null)
            StunRemaining.Value = stunReceiver.StunRemaining;

        if (StunPart != null)
            StunPart.Value = (int)stunReceiver.CurrentStunPart;

        if (DebugLog && Self != null && Self.Value != null)
        {
            Debug.Log(
                $"[UpdateEnemyStun] IsStun={stunReceiver.IsStunned}, " +
                $"Remaining={stunReceiver.StunRemaining}, " +
                $"Part={stunReceiver.CurrentStunPart}",
                Self.Value
            );
        }

        return Status.Success;
    }

    protected override void OnEnd()
    {
    }
}