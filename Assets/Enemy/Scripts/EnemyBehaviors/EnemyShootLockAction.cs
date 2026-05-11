using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Enemy Shoot Lock",
    story: "[Self] cannot shoot while [Condition] is true",
    category: "Enemy/Combat",
    id: "b2df2db9a6f94e0d9d0fb9e83c0f2a61"
)]
public partial class EnemyShootLockAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<bool> Condition;

    [Header("Condition")]
    [Tooltip("If true, the Condition value will be inverted before being used.")]
    [SerializeField] public bool InvertCondition = false;

    [Tooltip("Used when Condition is not assigned.")]
    [SerializeField] public bool ConditionNullValue = true;

    [Header("Component Search")]
    [SerializeField] public bool FindShootLockControllerOnRoot = true;
    [SerializeField] public bool FindWeaponShooterInChildren = true;
    [SerializeField] public bool IncludeInactiveWeaponShooter = true;
    [SerializeField] public bool FindAnimatorDriverOnRoot = true;

    [Header("Return")]
    [Tooltip("If true, this node returns Running while condition is true. Usually false if this node is used before Try In Order.")]
    [SerializeField] public bool ReturnRunningWhileConditionTrue = false;

    [Header("Lock")]
    [SerializeField] public bool UseShootLockController = true;

    [Tooltip("Directly sets EnemyWeaponShooter.externalShootLock = true while locked. Useful as fallback if ShootLockController is missing.")]
    [SerializeField] public bool DirectSetWeaponExternalShootLock = true;

    [SerializeField] public bool RestoreDirectWeaponExternalShootLockWhenConditionFalse = true;
    [SerializeField] public bool RemoveShootLockWhenConditionFalse = true;
    [SerializeField] public bool RemoveShootLockOnEnd = false;

    [Header("Clear Runtime")]
    [SerializeField] public bool ClearWeaponRuntimeState = true;
    [SerializeField] public bool ClearAnimatorShooting = true;

    [Header("Debug")]
    [SerializeField] public bool DebugLog = false;
    [SerializeField] public bool LogMissingComponents = false;

    private EnemyShootLockController shootLockController;
    private EnemyWeaponShooter weaponShooter;
    private EnemyAnimatorParameterDriver animatorDriver;

    private bool shootLockAdded;

    private bool directWeaponLockApplied;
    private bool cachedWeaponExternalShootLock;

    protected override Status OnStart()
    {
        if (Self == null || Self.Value == null)
            return Status.Failure;

        CacheComponents(Self.Value);

        bool conditionActive = IsConditionActive();

        if (conditionActive)
            ApplyShootLock();
        else if (RemoveShootLockWhenConditionFalse)
            RemoveShootLock();

        if (DebugLog)
        {
            Debug.Log(
                $"[EnemyShootLockAction] Start. Condition={conditionActive}, LockAdded={shootLockAdded}, DirectLock={directWeaponLockApplied}",
                Self.Value
            );
        }

        return ReturnRunningWhileConditionTrue && conditionActive
            ? Status.Running
            : Status.Success;
    }

    protected override Status OnUpdate()
    {
        if (Self == null || Self.Value == null)
            return Status.Failure;

        bool conditionActive = IsConditionActive();

        if (conditionActive)
        {
            ApplyShootLock();

            if (ReturnRunningWhileConditionTrue)
                return Status.Running;

            return Status.Success;
        }

        if (RemoveShootLockWhenConditionFalse)
            RemoveShootLock();

        return Status.Success;
    }

    protected override void OnEnd()
    {
        if (RemoveShootLockOnEnd)
            RemoveShootLock();

        if (DebugLog && Self != null && Self.Value != null)
        {
            Debug.Log(
                $"[EnemyShootLockAction] End. RemoveOnEnd={RemoveShootLockOnEnd}, LockAdded={shootLockAdded}, DirectLock={directWeaponLockApplied}",
                Self.Value
            );
        }
    }

    private void CacheComponents(GameObject self)
    {
        if (self == null)
            return;

        if (FindShootLockControllerOnRoot)
            shootLockController = self.GetComponent<EnemyShootLockController>();

        if (FindWeaponShooterInChildren)
            weaponShooter = self.GetComponentInChildren<EnemyWeaponShooter>(IncludeInactiveWeaponShooter);
        else
            weaponShooter = self.GetComponent<EnemyWeaponShooter>();

        if (FindAnimatorDriverOnRoot)
            animatorDriver = self.GetComponent<EnemyAnimatorParameterDriver>();
        else
            animatorDriver = self.GetComponentInChildren<EnemyAnimatorParameterDriver>(true);

        if (LogMissingComponents)
        {
            if (UseShootLockController && shootLockController == null)
                Debug.LogWarning("[EnemyShootLockAction] Missing EnemyShootLockController.", self);

            if (weaponShooter == null)
                Debug.LogWarning("[EnemyShootLockAction] Missing EnemyWeaponShooter.", self);

            if (ClearAnimatorShooting && animatorDriver == null)
                Debug.LogWarning("[EnemyShootLockAction] Missing EnemyAnimatorParameterDriver.", self);
        }
    }

    private bool IsConditionActive()
    {
        bool value = Condition != null ? Condition.Value : ConditionNullValue;

        if (InvertCondition)
            value = !value;

        return value;
    }

    private void ApplyShootLock()
    {
        if (UseShootLockController && shootLockController != null && !shootLockAdded)
        {
            shootLockController.AddShootLock();
            shootLockAdded = true;
        }

        if (weaponShooter != null)
        {
            if (DirectSetWeaponExternalShootLock)
            {
                if (!directWeaponLockApplied)
                {
                    cachedWeaponExternalShootLock = weaponShooter.externalShootLock;
                    directWeaponLockApplied = true;
                }

                weaponShooter.externalShootLock = true;
            }

            if (ClearWeaponRuntimeState)
                weaponShooter.ForceClearRuntimeState();
        }

        if (animatorDriver != null && ClearAnimatorShooting)
        {
            animatorDriver.SetShooting(false);
            animatorDriver.SetKeepShooting(false);
        }
    }

    private void RemoveShootLock()
    {
        if (shootLockAdded)
        {
            if (shootLockController != null)
                shootLockController.RemoveShootLock();

            shootLockAdded = false;
        }

        if (directWeaponLockApplied)
        {
            if (weaponShooter != null && RestoreDirectWeaponExternalShootLockWhenConditionFalse)
                weaponShooter.externalShootLock = cachedWeaponExternalShootLock;

            directWeaponLockApplied = false;
        }

        if (DebugLog && Self != null && Self.Value != null)
            Debug.Log("[EnemyShootLockAction] Shoot lock removed because condition is false.", Self.Value);
    }
}