using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Enemy Shoot At Transform",
    story: "[Self] shoots at [Target] for [ShootDuration] seconds",
    category: "Enemy/Combat",
    id: "d3b1ff04e6a74c2a9e5f083b4d2e7c91"
)]
public partial class EnemyShootAtTransformAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> Target;
    [SerializeReference] public BlackboardVariable<float> ShootDuration;

    [Header("Target Point")]
    [SerializeField] public bool LockTargetPositionOnStart = false;
    [SerializeField] public bool UseTargetVisionPoint = false;

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

        if (Target == null || Target.Value == null)
            return false;

        Transform t = Target.Value.transform;

        if (UseTargetVisionPoint)
        {
            EnemySensor sensor = Self?.Value?.GetComponent<EnemySensor>();
            if (sensor != null)
            {
                targetPoint = sensor.GetTargetVisionPosition();
                return true;
            }
        }

        targetPoint = t.position;
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
}
