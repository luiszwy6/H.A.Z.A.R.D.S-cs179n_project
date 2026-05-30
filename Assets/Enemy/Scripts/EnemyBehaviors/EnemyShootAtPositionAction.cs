using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Enemy Shoot At Position",
    story: "[Self] shoots at position [TargetPosition] for [ShootDuration] seconds",
    category: "Enemy/Combat",
    id: "c2a2ee93fa0640f1bd9b472ae3ad7d8b"
)]
public partial class EnemyShootAtPositionAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<Vector3> TargetPosition;
    [SerializeReference] public BlackboardVariable<float> ShootDuration;

    [Header("Last Known Position")]
    [SerializeField] public bool PreferSensorLastKnownPosition = true;
    [SerializeField] public bool RequireValidSensorLastKnownPosition = false;
    [SerializeField] public bool LockTargetPositionOnStart = true;

    [Header("Attack Adapter")]
    [SerializeField] public bool PreferMeleeAttacker = false;

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

    [Header("Result")]
    [SerializeField] public bool RequireAtLeastOneShot = true;

    private EnemyWeaponShooter shooter;
    private EnemyMeleeAttacker meleeAttacker;
    private EnemySensor sensor;

    private float startTime;
    private bool hasShot;
    private Vector3 lockedTargetPoint;
    private bool hasLockedTargetPoint;

    protected override Status OnStart()
    {
        if (Self == null || Self.Value == null)
            return Status.Failure;

        shooter = Self.Value.GetComponentInChildren<EnemyWeaponShooter>(true);
        meleeAttacker = Self.Value.GetComponentInChildren<EnemyMeleeAttacker>(true);
        sensor = Self.Value.GetComponent<EnemySensor>();

        if (shooter == null && meleeAttacker == null)
            return Status.Failure;

        hasLockedTargetPoint = false;

        if (LockTargetPositionOnStart)
        {
            if (!TryResolveTargetPoint(out lockedTargetPoint))
                return Status.Failure;

            hasLockedTargetPoint = true;
        }

        startTime = Time.time;
        hasShot = false;

        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (shooter == null && meleeAttacker == null)
            return Status.Failure;

        if (!TryGetCurrentTargetPoint(out Vector3 targetPoint))
            return Status.Failure;

        float duration = ResolveShootDuration();

        ApplyAimAndFacing(targetPoint);

        if (!CanShootFromAimGate(targetPoint))
        {
            startTime += Time.deltaTime;
            return Status.Running;
        }

        if (duration <= 0f)
            return TryShoot(targetPoint) ? Status.Success : Status.Failure;

        if (Time.time - startTime >= duration)
            return !RequireAtLeastOneShot || hasShot ? Status.Success : Status.Failure;

        if (TryShoot(targetPoint))
            hasShot = true;

        return Status.Running;
    }

    protected override void OnEnd()
    {
        if (shooter != null)
            shooter.ForceClearRuntimeState();

        if (meleeAttacker != null)
            meleeAttacker.ForceClearRuntimeState();

        if (Self?.Value != null)
        {
            UnityEngine.AI.NavMeshAgent agent =
                Self.Value.GetComponent<UnityEngine.AI.NavMeshAgent>();

            if (agent != null)
                agent.updateRotation = true;
        }
    }

    private bool TryShoot(Vector3 targetPoint)
    {
        if (!CanShootFromAimGate(targetPoint))
            return false;

        if (PreferMeleeAttacker && meleeAttacker != null)
            return meleeAttacker.ShootAt(targetPoint);

        if (shooter != null)
            return shooter.ShootAt(targetPoint);

        if (meleeAttacker != null)
            return meleeAttacker.ShootAt(targetPoint);

        return false;
    }

    private bool TryGetCurrentTargetPoint(out Vector3 targetPoint)
    {
        if (LockTargetPositionOnStart)
        {
            targetPoint = lockedTargetPoint;
            return hasLockedTargetPoint;
        }

        return TryResolveTargetPoint(out targetPoint);
    }

    private bool TryResolveTargetPoint(out Vector3 targetPoint)
    {
        targetPoint = Vector3.zero;

        if (PreferSensorLastKnownPosition)
        {
            if (sensor == null && Self != null && Self.Value != null)
                sensor = Self.Value.GetComponent<EnemySensor>();

            if (sensor != null)
            {
                if (sensor.HasLastKnownPosition)
                {
                    targetPoint = sensor.LastKnownPosition;
                    return true;
                }
            }

            if (RequireValidSensorLastKnownPosition)
                return false;
        }

        if (TargetPosition == null)
            return false;

        targetPoint = TargetPosition.Value;
        return true;
    }

    private float ResolveShootDuration()
    {
        if (ShootDuration == null)
            return 2f;

        return Mathf.Max(0f, ShootDuration.Value);
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
}
