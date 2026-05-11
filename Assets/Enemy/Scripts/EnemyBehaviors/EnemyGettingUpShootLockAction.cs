using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Enemy Getting Up Shoot Lock",
    story: "[Self] cannot shoot while getting up",
    category: "Enemy/State",
    id: "c8e29b9b5c2b4d52a6e9a3c0d33f8a41"
)]
public partial class EnemyGettingUpShootLockAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;

    [Header("Blackboard Output")]
    [SerializeReference] public BlackboardVariable<bool> IsGettingUp;
    [SerializeReference] public BlackboardVariable<float> GettingUpRemaining;
    [SerializeReference] public BlackboardVariable<bool> CanShoot;

    [Header("Search")]
    [SerializeField] public bool SearchInChildren = true;
    [SerializeField] public bool IncludeInactive = true;

    [Header("Can Shoot Blackboard")]
    [SerializeField] public bool WriteCanShootFalseWhileGettingUp = true;
    [SerializeField] public bool RestoreCanShootOnExit = true;
    [SerializeField] public bool RestoredCanShootValue = true;

    [Header("Shoot Lock")]
    [SerializeField] public bool UseShootLockController = true;
    [SerializeField] public bool DirectSetWeaponExternalShootLock = true;
    [SerializeField] public bool RestoreExternalShootLockOnExit = true;

    [Header("Clear Runtime While Getting Up")]
    [SerializeField] public bool ClearWeaponRuntimeState = true;
    [SerializeField] public bool ClearAnimatorShooting = true;

    [Header("Return")]
    [SerializeField] public bool ReturnFailureWhenNotGettingUp = true;

    [Header("Debug")]
    [SerializeField] public bool DebugLog = false;

    private EnemyRagdollGetUp ragdollGetUp;
    private EnemyShootLockController shootLockController;
    private EnemyWeaponShooter weaponShooter;
    private EnemyAnimatorParameterDriver animatorDriver;

    private bool shootLockAddedByThis;
    private bool cachedExternalShootLock;
    private bool hasCachedExternalShootLock;

    protected override Status OnStart()
    {
        if (Self == null || Self.Value == null)
            return Status.Failure;

        CacheReferences();

        return TickGettingUpLock();
    }

    protected override Status OnUpdate()
    {
        return TickGettingUpLock();
    }

    protected override void OnEnd()
    {
        RemoveShootLock();

        if (RestoreCanShootOnExit && CanShoot != null)
            CanShoot.Value = RestoredCanShootValue;
    }

    private Status TickGettingUpLock()
    {
        if (Self == null || Self.Value == null)
            return Status.Failure;

        if (ragdollGetUp == null)
            CacheReferences();

        if (ragdollGetUp == null)
        {
            WriteGettingUpBlackboard(false, 0f);
            RemoveShootLock();

            return Status.Failure;
        }

        bool gettingUp = ragdollGetUp.IsGettingUp;
        float remaining = ragdollGetUp.GettingUpRemaining;

        WriteGettingUpBlackboard(gettingUp, remaining);

        if (!gettingUp)
        {
            RemoveShootLock();

            if (RestoreCanShootOnExit && CanShoot != null)
                CanShoot.Value = RestoredCanShootValue;

            return ReturnFailureWhenNotGettingUp
                ? Status.Failure
                : Status.Success;
        }

        if (WriteCanShootFalseWhileGettingUp && CanShoot != null)
            CanShoot.Value = false;

        ApplyShootLock();

        if (DebugLog)
        {
            Debug.Log(
                $"[EnemyGettingUpShootLockAction] GettingUp=true, Remaining={remaining:F2}",
                Self.Value
            );
        }

        return Status.Running;
    }

    private void CacheReferences()
    {
        if (Self == null || Self.Value == null)
            return;

        GameObject self = Self.Value;

        ragdollGetUp = self.GetComponent<EnemyRagdollGetUp>();

        if (ragdollGetUp == null && SearchInChildren)
            ragdollGetUp = self.GetComponentInChildren<EnemyRagdollGetUp>(IncludeInactive);

        shootLockController = self.GetComponent<EnemyShootLockController>();

        if (shootLockController == null && SearchInChildren)
            shootLockController = self.GetComponentInChildren<EnemyShootLockController>(IncludeInactive);

        weaponShooter = self.GetComponentInChildren<EnemyWeaponShooter>(IncludeInactive);

        animatorDriver = self.GetComponent<EnemyAnimatorParameterDriver>();

        if (animatorDriver == null && SearchInChildren)
            animatorDriver = self.GetComponentInChildren<EnemyAnimatorParameterDriver>(IncludeInactive);
    }

    private void WriteGettingUpBlackboard(bool value, float remaining)
    {
        if (IsGettingUp != null)
            IsGettingUp.Value = value;

        if (GettingUpRemaining != null)
            GettingUpRemaining.Value = remaining;
    }

    private void ApplyShootLock()
    {
        if (UseShootLockController &&
            shootLockController != null &&
            !shootLockAddedByThis)
        {
            shootLockController.AddShootLock();
            shootLockAddedByThis = true;
        }

        if (weaponShooter != null)
        {
            if (DirectSetWeaponExternalShootLock)
            {
                if (!hasCachedExternalShootLock)
                {
                    cachedExternalShootLock = weaponShooter.externalShootLock;
                    hasCachedExternalShootLock = true;
                }
            }

            if (ClearWeaponRuntimeState)
                weaponShooter.ForceClearRuntimeState();

            if (DirectSetWeaponExternalShootLock)
                weaponShooter.externalShootLock = true;
        }

        if (animatorDriver != null && ClearAnimatorShooting)
        {
            animatorDriver.SetShooting(false);
            animatorDriver.SetKeepShooting(false);
        }
    }

    private void RemoveShootLock()
    {
        if (shootLockAddedByThis)
        {
            if (shootLockController != null)
                shootLockController.RemoveShootLock();

            shootLockAddedByThis = false;
        }

        if (weaponShooter != null &&
            hasCachedExternalShootLock &&
            RestoreExternalShootLockOnExit)
        {
            weaponShooter.externalShootLock = cachedExternalShootLock;
        }

        hasCachedExternalShootLock = false;
    }
}