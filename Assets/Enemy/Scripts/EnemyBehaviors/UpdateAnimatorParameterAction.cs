using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Update Enemy Animator Parameters",
    story: "[Self] updates animator parameters",
    category: "Enemy/Animation",
    id: "d8f8bb8e2a0d4a1b9441a5cc4b1fb8a1"
)]
public partial class UpdateEnemyAnimatorParametersAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;

    [Header("Movement Floats")]
    [SerializeReference] public BlackboardVariable<float> Speed;
    [SerializeReference] public BlackboardVariable<float> SpeedX;
    [SerializeReference] public BlackboardVariable<float> SpeedZ;

    [Header("Movement Bools")]
    [SerializeReference] public BlackboardVariable<bool> IsMoving;
    [SerializeReference] public BlackboardVariable<bool> IsWalking;
    [SerializeReference] public BlackboardVariable<bool> IsRunning;
    [SerializeReference] public BlackboardVariable<bool> IsIdle;
    [SerializeReference] public BlackboardVariable<bool> IsCrouching;

    [Header("Combat Bools")]
    [SerializeReference] public BlackboardVariable<bool> IsAiming;
    [SerializeReference] public BlackboardVariable<bool> IsShooting;
    [SerializeReference] public BlackboardVariable<bool> KeepShooting;

    [Header("Reload Bools")]
    [SerializeReference] public BlackboardVariable<bool> IsReloading;
    [SerializeReference] public BlackboardVariable<bool> KeepReloading;

    [Header("Weapon Bools")]
    [SerializeReference] public BlackboardVariable<bool> AssaultRifle;
    [SerializeReference] public BlackboardVariable<bool> ShotGun;
    [SerializeReference] public BlackboardVariable<bool> Sniper;
    [SerializeReference] public BlackboardVariable<bool> Pistol;

    [Header("Optional Player-Style Bools")]
    [SerializeReference] public BlackboardVariable<bool> IsSwitching;
    [SerializeReference] public BlackboardVariable<bool> QuickShot;
    [SerializeReference] public BlackboardVariable<bool> IsProne;
    [SerializeReference] public BlackboardVariable<bool> IsDiving;
    [SerializeReference] public BlackboardVariable<bool> IsSlide;

    [Header("Parameter Names")]
    [SerializeField] private string speedName = "Speed";
    [SerializeField] private string speedXName = "SpeedX";
    [SerializeField] private string speedZName = "SpeedZ";

    [SerializeField] private string isMovingName = "IsMoving";
    [SerializeField] private string isWalkingName = "IsWalking";
    [SerializeField] private string isRunningName = "IsRunning";
    [SerializeField] private string isIdleName = "IsIdle";
    [SerializeField] private string isCrouchingName = "IsCrouching";

    [SerializeField] private string isAimingName = "IsAiming";
    [SerializeField] private string isShootingName = "IsShooting";
    [SerializeField] private string keepShootingName = "KeepShooting";

    [SerializeField] private string isReloadingName = "IsReloading";
    [SerializeField] private string keepReloadingName = "KeepReloading";

    [SerializeField] private string assaultRifleName = "AssaultRifle";
    [SerializeField] private string shotGunName = "ShotGun";
    [SerializeField] private string sniperName = "Sniper";
    [SerializeField] private string pistolName = "Pistol";

    [SerializeField] private string isSwitchingName = "IsSwitching";
    [SerializeField] private string quickShotName = "QuickShot";
    [SerializeField] private string isProneName = "IsProne";
    [SerializeField] private string isDivingName = "IsDiving";
    [SerializeField] private string isSlideName = "IsSlide";

    [Header("Options")]
    [SerializeField] private bool writeDefaultValueIfParameterMissing = false;
    [SerializeField] private bool debugMissingParameters = false;

    private Animator animator;

    protected override Status OnStart()
    {
        if (Self == null || Self.Value == null)
            return Status.Failure;

        animator = Self.Value.GetComponentInChildren<Animator>();

        if (animator == null)
            return Status.Failure;

        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (animator == null)
            return Status.Failure;

        ReadFloat(speedName, Speed);
        ReadFloat(speedXName, SpeedX);
        ReadFloat(speedZName, SpeedZ);

        ReadBool(isMovingName, IsMoving);
        ReadBool(isWalkingName, IsWalking);
        ReadBool(isRunningName, IsRunning);
        ReadBool(isIdleName, IsIdle);
        ReadBool(isCrouchingName, IsCrouching);

        ReadBool(isAimingName, IsAiming);
        ReadBool(isShootingName, IsShooting);
        ReadBool(keepShootingName, KeepShooting);

        ReadBool(isReloadingName, IsReloading);
        ReadBool(keepReloadingName, KeepReloading);

        ReadBool(assaultRifleName, AssaultRifle);
        ReadBool(shotGunName, ShotGun);
        ReadBool(sniperName, Sniper);
        ReadBool(pistolName, Pistol);

        ReadBool(isSwitchingName, IsSwitching);
        ReadBool(quickShotName, QuickShot);
        ReadBool(isProneName, IsProne);
        ReadBool(isDivingName, IsDiving);
        ReadBool(isSlideName, IsSlide);

        return Status.Success;
    }

    protected override void OnEnd()
    {
    }

    private void ReadBool(string parameterName, BlackboardVariable<bool> output)
    {
        if (output == null)
            return;

        if (!HasParameter(parameterName, AnimatorControllerParameterType.Bool))
        {
            if (writeDefaultValueIfParameterMissing)
                output.Value = false;

            if (debugMissingParameters)
                Debug.LogWarning($"Animator bool parameter not found: {parameterName}", animator);

            return;
        }

        output.Value = animator.GetBool(parameterName);
    }

    private void ReadFloat(string parameterName, BlackboardVariable<float> output)
    {
        if (output == null)
            return;

        if (!HasParameter(parameterName, AnimatorControllerParameterType.Float))
        {
            if (writeDefaultValueIfParameterMissing)
                output.Value = 0f;

            if (debugMissingParameters)
                Debug.LogWarning($"Animator float parameter not found: {parameterName}", animator);

            return;
        }

        output.Value = animator.GetFloat(parameterName);
    }

    private bool HasParameter(string parameterName, AnimatorControllerParameterType type)
    {
        if (animator == null)
            return false;

        if (string.IsNullOrWhiteSpace(parameterName))
            return false;

        AnimatorControllerParameter[] parameters = animator.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];

            if (parameter.type == type && parameter.name == parameterName)
                return true;
        }

        return false;
    }
}