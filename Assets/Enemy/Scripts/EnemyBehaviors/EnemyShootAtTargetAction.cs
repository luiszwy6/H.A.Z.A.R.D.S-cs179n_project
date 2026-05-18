using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Enemy Shoot At Target",
    story: "[Self] shoots at [Target]",
    category: "Enemy/Combat",
    id: "e4fd75bb3fb041559c4c84d6e2dd1882"
)]
public partial class EnemyShootAtTargetAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> Target;
    [SerializeReference] public BlackboardVariable<float> TargetHeightOffset;

    [SerializeField] public bool RequireSensorCanSeeTarget = true;

    [Header("Attack Adapter")]
    [SerializeField] public bool PreferMeleeAttacker = false;

    private EnemyWeaponShooter shooter;
    private EnemyMeleeAttacker meleeAttacker;
    private EnemySensor sensor;

    protected override Status OnStart()
    {
        if (Self == null || Self.Value == null)
            return Status.Failure;

        shooter = Self.Value.GetComponentInChildren<EnemyWeaponShooter>(true);
        meleeAttacker = Self.Value.GetComponentInChildren<EnemyMeleeAttacker>(true);
        sensor = Self.Value.GetComponent<EnemySensor>();

        if (shooter == null && meleeAttacker == null)
            return Status.Failure;

        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (Target == null || Target.Value == null)
            return Status.Failure;

        if (shooter == null && meleeAttacker == null)
            return Status.Failure;

        if (RequireSensorCanSeeTarget && sensor != null)
        {
            sensor.RefreshSensor();

            if (!sensor.CanSeeTarget)
            {
                ForceClearAttackRuntimeState();
                return Status.Failure;
            }
        }

        float heightOffset = TargetHeightOffset != null ? TargetHeightOffset.Value : 1.3f;
        Vector3 targetPoint = Target.Value.transform.position + Vector3.up * heightOffset;

        bool usedAttack = false;

        if (PreferMeleeAttacker && meleeAttacker != null)
        {
            usedAttack = meleeAttacker.ShootAt(targetPoint);
        }
        else if (shooter != null)
        {
            usedAttack = shooter.ShootAt(targetPoint);
        }
        else if (meleeAttacker != null)
        {
            usedAttack = meleeAttacker.ShootAt(targetPoint);
        }

        return usedAttack ? Status.Success : Status.Failure;
    }

    protected override void OnEnd()
    {
        ForceClearAttackRuntimeState();
    }

    private void ForceClearAttackRuntimeState()
    {
        if (shooter != null)
            shooter.ForceClearRuntimeState();

        if (meleeAttacker != null)
            meleeAttacker.ForceClearRuntimeState();
    }
}