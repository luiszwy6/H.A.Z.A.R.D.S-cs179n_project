using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using UnityEngine.AI;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Aiming At Target",
    story: "[Self] aims at [Target], or position [TargetPosition]",
    category: "Enemy/Animation",
    id: "a9f3b73656b34c319d9ad71b2b1ed245"
)]
public partial class AimingAtAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> Target;

    [SerializeReference] public BlackboardVariable<Vector3> TargetPosition;
    [SerializeReference] public BlackboardVariable<bool> UseTargetPosition;

    [SerializeReference] public BlackboardVariable<float> RotationSpeed;
    [SerializeReference] public BlackboardVariable<float> AngleTolerance;

    [SerializeReference] public BlackboardVariable<bool> Aiming;
    [SerializeReference] public BlackboardVariable<bool> StopMoving;
    [SerializeReference] public BlackboardVariable<bool> DisableAgentRotation;
    [SerializeReference] public BlackboardVariable<bool> RestoreAgentRotationOnEnd;
    [SerializeReference] public BlackboardVariable<bool> ClearAimingOnEnd;
    [SerializeReference] public BlackboardVariable<EnemyMoveMode> MoveMode;

    [SerializeReference] public BlackboardVariable<bool> RequireStoppedBeforeSuccess;
    [SerializeReference] public BlackboardVariable<float> StopVelocityThreshold;
    [SerializeReference] public BlackboardVariable<float> MinimumAimReadyTime;

    [SerializeReference] public BlackboardVariable<bool> RequireAnimatorAiming;
    [SerializeReference] public BlackboardVariable<bool> RequireAnimatorNotMoving;
    [SerializeReference] public BlackboardVariable<bool> FailIfAnimatorParameterMissing;

    [SerializeField] private string isAimingBoolName = "IsAiming";
    [SerializeField] private string isMovingBoolName = "IsMoving";
    [SerializeField] private string isRunningBoolName = "IsRunning";

    private NavMeshAgent agent;
    private Animator animator;
    private EnemyAnimatorParameterDriver animatorDriver;

    private float aimReadyTimer;

    protected override Status OnStart()
    {
        if (Self == null || Self.Value == null)
            return Status.Failure;

        if (!HasValidAimTarget())
            return Status.Failure;

        agent = Self.Value.GetComponent<NavMeshAgent>();
        animator = Self.Value.GetComponentInChildren<Animator>();
        animatorDriver = Self.Value.GetComponent<EnemyAnimatorParameterDriver>();

        if (animatorDriver == null)
            return Status.Failure;

        aimReadyTimer = 0f;

        bool aiming = ResolveAiming();
        EnemyMoveMode moveMode = ResolveMoveMode();

        animatorDriver.SetAiming(aiming);

        if (moveMode != EnemyMoveMode.None)
            animatorDriver.SetMoveMode(moveMode);

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            if (ResolveStopMoving())
            {
                agent.isStopped = true;
                agent.ResetPath();
            }

            if (ResolveDisableAgentRotation())
                agent.updateRotation = false;
        }

        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (Self == null || Self.Value == null)
            return Status.Failure;

        if (!HasValidAimTarget())
            return Status.Failure;

        bool aiming = ResolveAiming();
        bool stopMoving = ResolveStopMoving();

        if (animatorDriver != null)
            animatorDriver.SetAiming(aiming);

        if (agent != null && agent.enabled && agent.isOnNavMesh && stopMoving)
        {
            agent.isStopped = true;

            if (agent.hasPath)
                agent.ResetPath();
        }

        Transform selfTransform = Self.Value.transform;
        Vector3 targetPoint = ResolveAimTargetPoint();

        Vector3 direction = targetPoint - selfTransform.position;
        direction.y = 0f;

        bool angleReady = true;

        if (direction.sqrMagnitude > 0.01f)
        {
            float rotationSpeed = ResolveRotationSpeed();
            float angleTolerance = ResolveAngleTolerance();

            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);

            selfTransform.rotation = Quaternion.Slerp(
                selfTransform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );

            float angle = Vector3.Angle(selfTransform.forward, direction.normalized);
            angleReady = angle <= angleTolerance;
        }

        bool stoppedReady = IsStoppedEnough();
        bool animatorReady = IsAnimatorReady();

        if (angleReady && stoppedReady && animatorReady)
        {
            aimReadyTimer += Time.deltaTime;

            if (aimReadyTimer >= ResolveMinimumAimReadyTime())
                return Status.Success;
        }
        else
        {
            aimReadyTimer = 0f;
        }

        return Status.Running;
    }

    protected override void OnEnd()
    {
        aimReadyTimer = 0f;

        if (animatorDriver != null && ResolveClearAimingOnEnd())
            animatorDriver.SetAiming(false);

        if (agent != null && ResolveRestoreAgentRotationOnEnd())
            agent.updateRotation = true;
    }

    private bool HasValidAimTarget()
    {
        if (ResolveUseTargetPosition())
            return TargetPosition != null;

        return Target != null && Target.Value != null;
    }

    private Vector3 ResolveAimTargetPoint()
    {
        if (ResolveUseTargetPosition())
            return TargetPosition.Value;

        return Target.Value.transform.position;
    }

    private bool IsStoppedEnough()
    {
        if (!ResolveRequireStoppedBeforeSuccess())
            return true;

        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return true;

        if (agent.pathPending)
            return false;

        if (agent.velocity.magnitude > ResolveStopVelocityThreshold())
            return false;

        return true;
    }

    private bool IsAnimatorReady()
    {
        if (animator == null)
            return true;

        if (ResolveRequireAnimatorAiming())
        {
            if (!TryGetAnimatorBool(isAimingBoolName, out bool isAiming))
                return !ResolveFailIfAnimatorParameterMissing();

            if (!isAiming)
                return false;
        }

        if (ResolveRequireAnimatorNotMoving())
        {
            if (TryGetAnimatorBool(isMovingBoolName, out bool isMoving))
            {
                if (isMoving)
                    return false;
            }
            else if (ResolveFailIfAnimatorParameterMissing())
            {
                return false;
            }

            if (TryGetAnimatorBool(isRunningBoolName, out bool isRunning))
            {
                if (isRunning)
                    return false;
            }
            else if (ResolveFailIfAnimatorParameterMissing())
            {
                return false;
            }
        }

        return true;
    }

    private bool TryGetAnimatorBool(string parameterName, out bool value)
    {
        value = false;

        if (animator == null)
            return false;

        if (string.IsNullOrWhiteSpace(parameterName))
            return false;

        AnimatorControllerParameter[] parameters = animator.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];

            if (parameter.type == AnimatorControllerParameterType.Bool &&
                parameter.name == parameterName)
            {
                value = animator.GetBool(parameterName);
                return true;
            }
        }

        return false;
    }

    private float ResolveRotationSpeed()
    {
        if (RotationSpeed == null)
            return 10f;

        return RotationSpeed.Value;
    }

    private float ResolveAngleTolerance()
    {
        if (AngleTolerance == null)
            return 3f;

        return AngleTolerance.Value;
    }

    private bool ResolveAiming()
    {
        if (Aiming == null)
            return true;

        return Aiming.Value;
    }

    private bool ResolveStopMoving()
    {
        if (StopMoving == null)
            return true;

        return StopMoving.Value;
    }

    private bool ResolveDisableAgentRotation()
    {
        if (DisableAgentRotation == null)
            return true;

        return DisableAgentRotation.Value;
    }

    private bool ResolveRestoreAgentRotationOnEnd()
    {
        if (RestoreAgentRotationOnEnd == null)
            return false;

        return RestoreAgentRotationOnEnd.Value;
    }

    private bool ResolveClearAimingOnEnd()
    {
        if (ClearAimingOnEnd == null)
            return false;

        return ClearAimingOnEnd.Value;
    }

    private EnemyMoveMode ResolveMoveMode()
    {
        if (MoveMode == null)
            return EnemyMoveMode.None;

        return MoveMode.Value;
    }

    private bool ResolveRequireStoppedBeforeSuccess()
    {
        if (RequireStoppedBeforeSuccess == null)
            return true;

        return RequireStoppedBeforeSuccess.Value;
    }

    private float ResolveStopVelocityThreshold()
    {
        if (StopVelocityThreshold == null)
            return 0.05f;

        return Mathf.Max(0f, StopVelocityThreshold.Value);
    }

    private float ResolveMinimumAimReadyTime()
    {
        if (MinimumAimReadyTime == null)
            return 0.25f;

        return Mathf.Max(0f, MinimumAimReadyTime.Value);
    }

    private bool ResolveRequireAnimatorAiming()
    {
        if (RequireAnimatorAiming == null)
            return true;

        return RequireAnimatorAiming.Value;
    }

    private bool ResolveRequireAnimatorNotMoving()
    {
        if (RequireAnimatorNotMoving == null)
            return true;

        return RequireAnimatorNotMoving.Value;
    }

    private bool ResolveFailIfAnimatorParameterMissing()
    {
        if (FailIfAnimatorParameterMissing == null)
            return false;

        return FailIfAnimatorParameterMissing.Value;
    }

    private bool ResolveUseTargetPosition()
    {
        if (UseTargetPosition == null)
            return false;

        return UseTargetPosition.Value;
    }
}