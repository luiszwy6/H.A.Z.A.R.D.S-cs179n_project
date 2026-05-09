using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using UnityEngine.AI;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Clear Enemy Combat",
    story: "[Self] clears enemy combat state",
    category: "Enemy/Animation",
    id: "f3d251a9a9cc4df09ef69f3d7b5ac918"
)]
public partial class ClearEnemyCombatAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;

    [SerializeField] public bool RestoreAgentRotation = true;
    [SerializeField] public bool StopMoving = false;
    [SerializeField] public EnemyMoveMode MoveMode = EnemyMoveMode.Walk;

    protected override Status OnUpdate()
    {
        if (Self == null || Self.Value == null)
            return Status.Failure;

        EnemyAnimatorParameterDriver animatorDriver =
            Self.Value.GetComponent<EnemyAnimatorParameterDriver>();

        if (animatorDriver == null)
            animatorDriver = Self.Value.GetComponentInChildren<EnemyAnimatorParameterDriver>();

        if (animatorDriver != null)
        {
            animatorDriver.ClearCombat();
            animatorDriver.SetMoveMode(MoveMode);
        }

        NavMeshAgent agent = Self.Value.GetComponent<NavMeshAgent>();

        if (agent != null)
        {
            if (RestoreAgentRotation)
                agent.updateRotation = true;

            if (StopMoving)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
        }

        return Status.Success;
    }
}