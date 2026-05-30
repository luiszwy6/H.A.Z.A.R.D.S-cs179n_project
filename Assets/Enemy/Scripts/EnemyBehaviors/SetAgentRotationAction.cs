using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using UnityEngine.AI;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Set Agent Rotation",
    story: "[Self] sets agent rotation to [UpdateRotation]",
    category: "Enemy/Movement",
    id: "d2f3f40f8bb8441d99a906f2f19e0d4b"
)]
public partial class SetAgentRotationAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<bool> UpdateRotation;

    protected override Status OnStart()
    {
        if (Self == null || Self.Value == null)
            return Status.Failure;

        NavMeshAgent agent = Self.Value.GetComponent<NavMeshAgent>();

        if (agent == null)
            return Status.Failure;

        agent.updateRotation = ResolveUpdateRotation();
        return Status.Success;
    }

    private bool ResolveUpdateRotation()
    {
        if (UpdateRotation != null)
            return UpdateRotation.Value;

        return false;
    }
}
