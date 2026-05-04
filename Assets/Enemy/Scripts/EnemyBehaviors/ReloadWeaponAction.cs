using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using UnityEngine.AI;
using Action = Unity.Behavior.Action;

public enum EnemyReloadMoveMode
{
    Walk,
    Crouch
}

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Reload Enemy Weapon",
    story: "[Self] reloads enemy weapon, can move [CanMoveWhileReloading]",
    category: "Enemy/Combat",
    id: "a8472ff5eac44a4e919a4b0bc9af0e42"
)]
public partial class ReloadWeaponAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> WeaponObject;

    [SerializeReference] public BlackboardVariable<bool> CanMoveWhileReloading;
    [SerializeReference] public BlackboardVariable<EnemyReloadMoveMode> ReloadMoveMode;

    [SerializeReference] public BlackboardVariable<bool> ClearAimingWhileReloading;
    [SerializeReference] public BlackboardVariable<bool> RestoreAgentRotation;
    [SerializeReference] public BlackboardVariable<bool> ApplyWeaponAnimatorStateBeforeReload;

    private EnemyWeaponSettings enemyWeaponSettings;
    private EnemyAnimatorParameterDriver animatorDriver;
    private NavMeshAgent agent;
    private EnemyWeaponShooter enemyWeaponShooter;
    private EnemyWeaponAnimatorState weaponAnimatorState;

    protected override Status OnStart()
    {
        if (Self == null || Self.Value == null)
            return Status.Failure;

        enemyWeaponSettings = ResolveEnemyWeaponSettings();

        if (enemyWeaponSettings == null)
            return Status.Failure;

        animatorDriver = Self.Value.GetComponent<EnemyAnimatorParameterDriver>();
        agent = Self.Value.GetComponent<NavMeshAgent>();
        enemyWeaponShooter = Self.Value.GetComponentInChildren<EnemyWeaponShooter>(true);
        weaponAnimatorState = ResolveWeaponAnimatorState();

        if (enemyWeaponSettings.IsReloading)
            return Status.Running;

        if (enemyWeaponSettings.IsMagazineFull)
            return Status.Failure;

        if (!enemyWeaponSettings.CanReload())
            return Status.Failure;

        if (enemyWeaponShooter != null)
            enemyWeaponShooter.ForceClearRuntimeState();

        if (weaponAnimatorState != null && ResolveApplyWeaponAnimatorStateBeforeReload())
            weaponAnimatorState.ApplyWeaponAnimatorState();

        if (animatorDriver != null)
        {
            animatorDriver.SetShooting(false);
            animatorDriver.SetKeepShooting(false);
            animatorDriver.SetMoveMode(ResolveEnemyMoveMode());

            if (ResolveClearAimingWhileReloading())
                animatorDriver.SetAiming(false);
        }

        ApplyMovementRule();

        bool startedReload = enemyWeaponSettings.TryStartReload();

        if (!startedReload)
            return Status.Failure;

        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (enemyWeaponSettings == null)
            return Status.Failure;

        if (animatorDriver != null)
        {
            animatorDriver.SetMoveMode(ResolveEnemyMoveMode());

            if (ResolveClearAimingWhileReloading())
                animatorDriver.SetAiming(false);

            animatorDriver.SetShooting(false);
            animatorDriver.SetKeepShooting(false);
        }

        ApplyMovementRule();

        if (enemyWeaponSettings.IsReloading)
            return Status.Running;

        if (enemyWeaponSettings.IsMagazineFull)
            return Status.Success;

        if (enemyWeaponSettings.CanShoot())
            return Status.Success;

        return Status.Failure;
    }

    protected override void OnEnd()
    {
    }

    private void ApplyMovementRule()
    {
        if (agent == null)
            return;

        if (ResolveCanMoveWhileReloading())
        {
            agent.isStopped = false;

            if (ResolveRestoreAgentRotation())
                agent.updateRotation = true;

            return;
        }

        agent.isStopped = true;
        agent.ResetPath();

        if (ResolveRestoreAgentRotation())
            agent.updateRotation = true;
    }

    private EnemyWeaponSettings ResolveEnemyWeaponSettings()
    {
        if (WeaponObject != null && WeaponObject.Value != null)
        {
            EnemyWeaponSettings weaponSettings =
                WeaponObject.Value.GetComponentInChildren<EnemyWeaponSettings>(true);

            if (weaponSettings != null)
                return weaponSettings;
        }

        return Self.Value.GetComponentInChildren<EnemyWeaponSettings>(true);
    }

    private EnemyWeaponAnimatorState ResolveWeaponAnimatorState()
    {
        if (WeaponObject != null && WeaponObject.Value != null)
        {
            EnemyWeaponAnimatorState state =
                WeaponObject.Value.GetComponentInChildren<EnemyWeaponAnimatorState>(true);

            if (state != null)
                return state;
        }

        return Self.Value.GetComponentInChildren<EnemyWeaponAnimatorState>(true);
    }

    private bool ResolveCanMoveWhileReloading()
    {
        if (CanMoveWhileReloading == null)
            return false;

        return CanMoveWhileReloading.Value;
    }

    private EnemyReloadMoveMode ResolveReloadMoveMode()
    {
        if (ReloadMoveMode == null)
            return EnemyReloadMoveMode.Walk;

        return ReloadMoveMode.Value;
    }

    private EnemyMoveMode ResolveEnemyMoveMode()
    {
        EnemyReloadMoveMode reloadMode = ResolveReloadMoveMode();

        switch (reloadMode)
        {
            case EnemyReloadMoveMode.Crouch:
                return EnemyMoveMode.Crouch;

            default:
                return EnemyMoveMode.Walk;
        }
    }

    private bool ResolveClearAimingWhileReloading()
    {
        if (ClearAimingWhileReloading == null)
            return true;

        return ClearAimingWhileReloading.Value;
    }

    private bool ResolveRestoreAgentRotation()
    {
        if (RestoreAgentRotation == null)
            return true;

        return RestoreAgentRotation.Value;
    }

    private bool ResolveApplyWeaponAnimatorStateBeforeReload()
    {
        if (ApplyWeaponAnimatorStateBeforeReload == null)
            return true;

        return ApplyWeaponAnimatorStateBeforeReload.Value;
    }
}