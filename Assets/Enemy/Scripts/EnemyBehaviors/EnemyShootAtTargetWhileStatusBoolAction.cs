using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Enemy Shoot At Target While Status Bool",
    story: "[Self] shoots at [Target] while enemy status [StatusCheck]",
    category: "Enemy/Combat",
    id: "b4b772b54b6d4fb6a8ab2c036d3d5591"
)]
public partial class EnemyShootAtTargetWhileStatusBoolAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> Target;
    [SerializeReference] public BlackboardVariable<EnemyStatus> EnemyStatus;
    [SerializeReference] public BlackboardVariable<EnemyStatusBoolCheck> StatusCheck;
    [SerializeReference] public BlackboardVariable<bool> InvertStatus;
    [SerializeReference] public BlackboardVariable<float> TargetHeightOffset;

    [SerializeField] public bool RequireSensorCanSeeTarget = true;
    [SerializeField] public bool ReturnSuccessWhenStatusEnds = true;
    [SerializeField] public bool ReturnFailureWhenReloadNeeded = true;

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
    private EnemyStatus enemyStatus;
    private EnemyWeaponSettings weaponSettings;

    protected override Status OnStart()
    {
        if (Self == null || Self.Value == null)
            return Status.Failure;

        shooter = Self.Value.GetComponentInChildren<EnemyWeaponShooter>(true);
        meleeAttacker = Self.Value.GetComponentInChildren<EnemyMeleeAttacker>(true);
        sensor = Self.Value.GetComponent<EnemySensor>();
        enemyStatus = ResolveEnemyStatus();
        weaponSettings = Self.Value.GetComponentInChildren<EnemyWeaponSettings>(true);

        if (shooter == null && meleeAttacker == null)
            return Status.Failure;

        if (enemyStatus == null)
            return Status.Failure;

        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (Target == null || Target.Value == null)
            return Status.Failure;

        if (shooter == null && meleeAttacker == null)
            return Status.Failure;

        if (!IsStatusActive())
        {
            ForceClearAttackRuntimeState();
            return ReturnSuccessWhenStatusEnds ? Status.Success : Status.Failure;
        }

        if (ShouldYieldToReload())
        {
            ForceClearAttackRuntimeState();
            return Status.Failure;
        }

        if (RequireSensorCanSeeTarget && sensor != null)
        {
            sensor.RefreshSensor();

            if (!sensor.CanSeeTarget)
            {
                ForceClearAttackRuntimeState();
                return Status.Running;
            }
        }

        float heightOffset = TargetHeightOffset != null ? TargetHeightOffset.Value : 1.3f;
        Vector3 targetPoint = Target.Value.transform.position + Vector3.up * heightOffset;

        ApplyAimAndFacing(targetPoint);

        if (!CanShootFromAimGate(targetPoint))
        {
            ForceClearAttackRuntimeState();
            return Status.Running;
        }

        if (PreferMeleeAttacker && meleeAttacker != null)
        {
            meleeAttacker.ShootAt(targetPoint);
        }
        else if (shooter != null)
        {
            shooter.ShootAt(targetPoint);
        }
        else if (meleeAttacker != null)
        {
            meleeAttacker.ShootAt(targetPoint);
        }

        return Status.Running;
    }

    protected override void OnEnd()
    {
        ForceClearAttackRuntimeState();
    }

    private EnemyStatus ResolveEnemyStatus()
    {
        if (EnemyStatus != null && EnemyStatus.Value != null)
            return EnemyStatus.Value;

        if (Self == null || Self.Value == null)
            return null;

        return Self.Value.GetComponent<EnemyStatus>();
    }

    private bool IsStatusActive()
    {
        if (enemyStatus == null)
            enemyStatus = ResolveEnemyStatus();

        if (enemyStatus == null)
            return false;

        bool result = CheckStatus(enemyStatus, ResolveStatusCheck());

        if (ResolveInvertStatus())
            result = !result;

        return result;
    }

    private bool CheckStatus(EnemyStatus status, EnemyStatusBoolCheck statusCheck)
    {
        switch (statusCheck)
        {
            case EnemyStatusBoolCheck.IsInCover:
                return status.IsInCover;

            case EnemyStatusBoolCheck.IsShooting:
                return status.IsShooting;

            case EnemyStatusBoolCheck.IsReloading:
                return status.IsReloading;

            case EnemyStatusBoolCheck.CanSeeTarget:
                return status.CanSeeTarget;

            case EnemyStatusBoolCheck.IsSmokeBlockingVision:
                return status.IsSmokeBlockingVision;

            case EnemyStatusBoolCheck.IsFlashBangStun:
                return status.IsFlashBangStun;

            case EnemyStatusBoolCheck.IsFlanking:
                return status.IsFlanking;

            case EnemyStatusBoolCheck.IsEscapingFromGrenade:
                return status.IsEscapingFromGrenade;

            case EnemyStatusBoolCheck.IsFollowingShield:
                return status.IsFollowingShield;

            case EnemyStatusBoolCheck.IsBackingAway:
                return status.IsBackingAway;

            case EnemyStatusBoolCheck.IsCombatStrafing:
                return status.IsCombatStrafing;

            case EnemyStatusBoolCheck.IsMeleeCombating:
                return status.IsMeleeCombating;

            case EnemyStatusBoolCheck.IsGoingToLKP:
                return status.IsGoingToLKP;

            case EnemyStatusBoolCheck.IsPatrol:
                return status.IsPatrol;

            case EnemyStatusBoolCheck.IsCoveringTeammate:
            default:
                return status.IsCoveringTeammate;
        }
    }

    private EnemyStatusBoolCheck ResolveStatusCheck()
    {
        if (StatusCheck == null)
            return EnemyStatusBoolCheck.IsCoveringTeammate;

        return StatusCheck.Value;
    }

    private bool ResolveInvertStatus()
    {
        if (InvertStatus == null)
            return false;

        return InvertStatus.Value;
    }

    private bool ShouldYieldToReload()
    {
        if (!ReturnFailureWhenReloadNeeded)
            return false;

        if (weaponSettings == null)
            return false;

        if (weaponSettings.IsReloading)
            return false;

        if (!weaponSettings.CanReload())
            return false;

        return weaponSettings.IsMagazineEmpty || !weaponSettings.CanShoot();
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
            EnemyShootAimGate.ResolveBool(FaceTargetBeforeShooting, false),
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

    private void ForceClearAttackRuntimeState()
    {
        if (shooter != null)
            shooter.ForceClearRuntimeState();

        if (meleeAttacker != null)
            meleeAttacker.ForceClearRuntimeState();
    }
}
