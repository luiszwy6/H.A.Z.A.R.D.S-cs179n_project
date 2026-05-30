using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Set Continuous Face Target",
    story: "[Self] sets continuous face [Enabled] toward [Target]",
    category: "Enemy/Movement",
    id: "f2e4a74d7c584848bd1d2731f355c83b"
)]
public partial class SetContinuousFaceTargetAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> Target;
    [SerializeReference] public BlackboardVariable<bool> Enabled;
    [SerializeReference] public BlackboardVariable<float> RotationSpeed;
    [SerializeReference] public BlackboardVariable<bool> UsePlayerStatusIfTargetMissing;
    [SerializeReference] public BlackboardVariable<bool> DisableAgentRotationWhileActive;
    [SerializeReference] public BlackboardVariable<bool> RestoreAgentRotationOnStop;
    [SerializeReference] public BlackboardVariable<bool> SetAiming;
    [SerializeReference] public BlackboardVariable<bool> Aiming;
    [SerializeReference] public BlackboardVariable<bool> ClearAimingOnStop;

    protected override Status OnUpdate()
    {
        if (Self == null || Self.Value == null)
            return Status.Failure;

        EnemyContinuousFaceTargetController controller =
            Self.Value.GetComponent<EnemyContinuousFaceTargetController>();

        if (controller == null)
            controller = Self.Value.AddComponent<EnemyContinuousFaceTargetController>();

        if (!ResolveEnabled())
        {
            controller.StopFacing();
            return Status.Success;
        }

        Transform targetTransform =
            Target != null && Target.Value != null
                ? Target.Value.transform
                : null;

        controller.StartFacing(
            targetTransform,
            ResolveRotationSpeed(),
            ResolveUsePlayerStatusIfTargetMissing(),
            ResolveDisableAgentRotationWhileActive(),
            ResolveRestoreAgentRotationOnStop(),
            ResolveSetAiming(),
            ResolveAiming(),
            ResolveClearAimingOnStop()
        );

        return Status.Success;
    }

    private bool ResolveEnabled()
    {
        if (Enabled == null)
            return true;

        return Enabled.Value;
    }

    private float ResolveRotationSpeed()
    {
        if (RotationSpeed == null)
            return 12f;

        return Mathf.Max(0f, RotationSpeed.Value);
    }

    private bool ResolveUsePlayerStatusIfTargetMissing()
    {
        if (UsePlayerStatusIfTargetMissing == null)
            return true;

        return UsePlayerStatusIfTargetMissing.Value;
    }

    private bool ResolveDisableAgentRotationWhileActive()
    {
        if (DisableAgentRotationWhileActive == null)
            return true;

        return DisableAgentRotationWhileActive.Value;
    }

    private bool ResolveRestoreAgentRotationOnStop()
    {
        if (RestoreAgentRotationOnStop == null)
            return false;

        return RestoreAgentRotationOnStop.Value;
    }

    private bool ResolveSetAiming()
    {
        if (SetAiming == null)
            return false;

        return SetAiming.Value;
    }

    private bool ResolveAiming()
    {
        if (Aiming == null)
            return true;

        return Aiming.Value;
    }

    private bool ResolveClearAimingOnStop()
    {
        if (ClearAimingOnStop == null)
            return false;

        return ClearAimingOnStop.Value;
    }
}
