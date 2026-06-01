using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Enemy Shoot At Target While Can Shoot",
    story: "[Self] shoots at [Target] while weapon can shoot",
    category: "Enemy/Combat",
    id: "c85a53f1d6d74158b4f75f49c83d5c12"
)]
public partial class EnemyShootAtTargetWhileCanShootAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> Target;
    [SerializeReference] public BlackboardVariable<float> TargetHeightOffset;

    [SerializeReference] public BlackboardVariable<bool> RequireSensorCanSeeTarget;
    [SerializeReference] public BlackboardVariable<bool> ReturnSuccessWhenCannotShoot;
    [SerializeReference] public BlackboardVariable<bool> ReturnRunningWhileCanShoot;

    [Header("Aim Control")]
    [SerializeReference] public BlackboardVariable<bool> SetAnimatorAiming;
    [SerializeReference] public BlackboardVariable<bool> Aiming;
    [SerializeReference] public BlackboardVariable<bool> FaceTargetBeforeShooting;
    [SerializeReference] public BlackboardVariable<float> FaceRotationSpeed;
    [SerializeReference] public BlackboardVariable<bool> DisableAgentRotationWhileShooting;

    [Header("Aim Gate")]
    [SerializeReference] public BlackboardVariable<bool> RequireAnimatorAiming;
    [SerializeReference] public BlackboardVariable<bool> RequireFacingTarget;
    [SerializeReference] public BlackboardVariable<float> AimAngleTolerance;
    [SerializeReference] public BlackboardVariable<bool> FailIfAimingParameterMissing;
    [SerializeField] private string isAimingBoolName = "IsAiming";

    [Header("Attack Adapter")]
    [SerializeField] public bool PreferMeleeAttacker = false;

    private EnemyWeaponShooter shooter;
    private EnemyMeleeAttacker meleeAttacker;
    private EnemySensor sensor;
    private EnemyWeaponSettings weaponSettings;
    private bool clearRuntimeStateOnEnd = true;

    protected override Status OnStart()
    {
        if (Self == null || Self.Value == null)
            return Status.Failure;

        clearRuntimeStateOnEnd = true;

        shooter = Self.Value.GetComponentInChildren<EnemyWeaponShooter>(true);
        meleeAttacker = Self.Value.GetComponentInChildren<EnemyMeleeAttacker>(true);
        sensor = Self.Value.GetComponent<EnemySensor>();
        weaponSettings = Self.Value.GetComponentInChildren<EnemyWeaponSettings>(true);

        if (shooter == null && meleeAttacker == null)
            return Status.Failure;

        if (weaponSettings == null && shooter != null)
            weaponSettings = shooter.GetComponentInChildren<EnemyWeaponSettings>(true);

        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (Target == null || Target.Value == null)
            return Status.Failure;

        if (shooter == null && meleeAttacker == null)
            return Status.Failure;

        if (!CanContinueAttack())
        {
            clearRuntimeStateOnEnd = true;
            ForceClearAttackRuntimeState();
            return EnemyShootAimGate.ResolveBool(ReturnSuccessWhenCannotShoot, false) ? Status.Success : Status.Failure;
        }

        if (EnemyShootAimGate.ResolveBool(RequireSensorCanSeeTarget, true) && sensor != null)
        {
            sensor.RefreshSensor();

            if (!sensor.CanSeeTarget)
            {
                clearRuntimeStateOnEnd = true;
                ForceClearAttackRuntimeState();
                return Status.Failure;
            }
        }

        float heightOffset = TargetHeightOffset != null ? TargetHeightOffset.Value : 1.3f;
        Vector3 targetPoint = Target.Value.transform.position + Vector3.up * heightOffset;

        ApplyAimAndFacing(targetPoint);

        if (!CanShootFromAimGate(targetPoint))
        {
            clearRuntimeStateOnEnd = !ReturnSuccessWhenActive();
            return ReturnSuccessWhenActive() ? Status.Success : Status.Running;
        }

        if (PreferMeleeAttacker && meleeAttacker != null)
        {
            meleeAttacker.ShootAt(targetPoint);
        }
        else if (shooter != null)
        {
            if (shooter.CanShoot())
                shooter.ShootAt(targetPoint);
        }
        else if (meleeAttacker != null)
        {
            meleeAttacker.ShootAt(targetPoint);
        }

        clearRuntimeStateOnEnd = !ReturnSuccessWhenActive();
        return ReturnSuccessWhenActive() ? Status.Success : Status.Running;
    }

    protected override void OnEnd()
    {
        if (clearRuntimeStateOnEnd)
            ForceClearAttackRuntimeState();
    }

    private bool CanContinueAttack()
    {
        if (PreferMeleeAttacker && meleeAttacker != null)
            return true;

        if (shooter == null)
            return meleeAttacker != null;

        if (weaponSettings == null)
            weaponSettings = shooter.GetEnemyWeaponSettings();

        if (weaponSettings == null)
            return true;

        if (weaponSettings.IsReloading)
            return false;

        if (weaponSettings.IsMagazineEmpty)
            return false;

        return true;
    }

    private bool CanShootFromAimGate(Vector3 targetPoint)
    {
        return EnemyShootAimGate.CanShoot(
            Self.Value,
            targetPoint,
            ResolveRequireAnimatorAiming(),
            ResolveRequireFacingTarget(),
            ResolveAimAngleTolerance(),
            isAimingBoolName,
            ResolveFailIfAimingParameterMissing()
        );
    }

    private void ApplyAimAndFacing(Vector3 targetPoint)
    {
        EnemyShootAimGate.ApplyAimAndFacing(
            Self.Value,
            targetPoint,
            EnemyShootAimGate.ResolveBool(SetAnimatorAiming, false),
            EnemyShootAimGate.ResolveBool(Aiming, true),
            EnemyShootAimGate.ResolveBool(FaceTargetBeforeShooting, true),
            EnemyShootAimGate.ResolveFloat(FaceRotationSpeed, 12f),
            EnemyShootAimGate.ResolveBool(DisableAgentRotationWhileShooting, true)
        );
    }

    private bool ResolveRequireAnimatorAiming()
    {
        if (RequireAnimatorAiming == null)
            return false;

        return RequireAnimatorAiming.Value;
    }

    private bool ResolveRequireFacingTarget()
    {
        if (RequireFacingTarget == null)
            return false;

        return RequireFacingTarget.Value;
    }

    private float ResolveAimAngleTolerance()
    {
        if (AimAngleTolerance == null)
            return 5f;

        return Mathf.Max(0f, AimAngleTolerance.Value);
    }

    private bool ResolveFailIfAimingParameterMissing()
    {
        if (FailIfAimingParameterMissing == null)
            return false;

        return FailIfAimingParameterMissing.Value;
    }

    private bool ReturnSuccessWhenActive()
    {
        return !EnemyShootAimGate.ResolveBool(ReturnRunningWhileCanShoot, false);
    }

    private void ForceClearAttackRuntimeState()
    {
        if (shooter != null)
            shooter.ForceClearRuntimeState();

        if (meleeAttacker != null)
            meleeAttacker.ForceClearRuntimeState();
    }
}
