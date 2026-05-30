using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using UnityEngine.AI;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Stop Facing And Use Agent Rotation",
    story: "[Self] stops facing target and uses agent rotation",
    category: "Enemy/Movement",
    id: "b2d327bfb0d24bdb9bb1f25fa0ce5577"
)]
public partial class StopFacingAndUseAgentRotationAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<bool> ClearAiming;

    protected override Status OnStart()
    {
        if (Self == null || Self.Value == null)
            return Status.Failure;

        GameObject self = Self.Value;

        EnemyContinuousFaceTargetController faceController =
            self.GetComponent<EnemyContinuousFaceTargetController>();

        if (faceController != null)
            faceController.StopFacing();

        NavMeshAgent agent = self.GetComponent<NavMeshAgent>();

        if (agent != null)
            agent.updateRotation = true;

        if (ResolveClearAiming())
        {
            EnemyAnimatorParameterDriver animatorDriver =
                self.GetComponent<EnemyAnimatorParameterDriver>();

            if (animatorDriver != null)
                animatorDriver.SetAiming(false);
        }

        return Status.Success;
    }

    private bool ResolveClearAiming()
    {
        if (ClearAiming == null)
            return false;

        return ClearAiming.Value;
    }
}
