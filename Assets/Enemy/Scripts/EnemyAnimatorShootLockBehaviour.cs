using UnityEngine;

public class EnemyAnimatorShootLockBehaviour : StateMachineBehaviour
{
    [Header("Lock Timing")]
    [SerializeField] private bool lockOnEnter = true;
    [SerializeField] private bool keepLockedWhileInState = true;
    [SerializeField] private bool unlockOnExit = true;
    [SerializeField] private bool unlockWhenAnimatorBoolFalse = true;
    [SerializeField] private string unlockAnimatorBoolName = "IsStun";

    [Header("Clear Runtime")]
    [SerializeField] private bool clearWeaponRuntimeOnEnter = true;
    [SerializeField] private bool clearWeaponRuntimeWhileInState = true;
    [SerializeField] private bool clearAnimatorShootingWhileInState = true;

    [Header("Search")]
    [SerializeField] private bool searchInParent = true;
    [SerializeField] private bool searchInChildrenFromRoot = true;
    [SerializeField] private bool includeInactiveChildren = true;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;

    private EnemyShootLockController lockController;
    private EnemyWeaponShooter weaponShooter;
    private EnemyAnimatorParameterDriver animatorDriver;

    private bool lockedByThisState;
    private int unlockAnimatorBoolHash;

    public override void OnStateEnter(
        Animator animator,
        AnimatorStateInfo stateInfo,
        int layerIndex
    )
    {
        CacheReferences(animator);
        CacheAnimatorHashes();

        if (lockOnEnter)
            AddLock(animator);

        if (clearWeaponRuntimeOnEnter)
            ClearRuntimeState();

        if (debugLog)
            Debug.Log($"[EnemyAnimatorShootLockBehaviour] Enter state: {stateInfo.shortNameHash}", animator);
    }

    public override void OnStateUpdate(
        Animator animator,
        AnimatorStateInfo stateInfo,
        int layerIndex
    )
    {
        CacheAnimatorHashes();

        if (ShouldUnlockFromAnimatorBool(animator))
        {
            RemoveLock(animator);
            return;
        }

        if (keepLockedWhileInState)
            AddLock(animator);

        if (clearWeaponRuntimeWhileInState)
            ClearRuntimeState();

        if (clearAnimatorShootingWhileInState)
            ClearAnimatorShooting();
    }

    public override void OnStateExit(
        Animator animator,
        AnimatorStateInfo stateInfo,
        int layerIndex
    )
    {
        if (!unlockOnExit)
            return;

        RemoveLock(animator);

        if (debugLog)
            Debug.Log($"[EnemyAnimatorShootLockBehaviour] Exit state: {stateInfo.shortNameHash}", animator);
    }

    private void CacheAnimatorHashes()
    {
        if (unlockAnimatorBoolHash != 0)
            return;

        if (string.IsNullOrWhiteSpace(unlockAnimatorBoolName))
            return;

        unlockAnimatorBoolHash = Animator.StringToHash(unlockAnimatorBoolName);
    }

    private bool ShouldUnlockFromAnimatorBool(Animator animator)
    {
        if (!unlockWhenAnimatorBoolFalse)
            return false;

        if (!lockedByThisState)
            return false;

        if (animator == null)
            return false;

        if (unlockAnimatorBoolHash == 0)
            return false;

        if (!HasAnimatorBool(animator, unlockAnimatorBoolHash))
            return false;

        return !animator.GetBool(unlockAnimatorBoolHash);
    }

    private bool HasAnimatorBool(Animator animator, int parameterHash)
    {
        AnimatorControllerParameter[] parameters = animator.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];

            if (parameter.nameHash == parameterHash &&
                parameter.type == AnimatorControllerParameterType.Bool)
            {
                return true;
            }
        }

        return false;
    }

    private void CacheReferences(Animator animator)
    {
        if (animator == null)
            return;

        if (lockController == null)
        {
            if (searchInParent)
                lockController = animator.GetComponentInParent<EnemyShootLockController>();

            if (lockController == null && searchInChildrenFromRoot)
            {
                Transform root = animator.transform.root;
                lockController = root.GetComponentInChildren<EnemyShootLockController>(includeInactiveChildren);
            }
        }

        if (weaponShooter == null)
        {
            if (searchInParent)
            {
                EnemyShootLockController parentLock =
                    animator.GetComponentInParent<EnemyShootLockController>();

                if (parentLock != null)
                    weaponShooter = parentLock.GetComponentInChildren<EnemyWeaponShooter>(includeInactiveChildren);
            }

            if (weaponShooter == null && searchInChildrenFromRoot)
            {
                Transform root = animator.transform.root;
                weaponShooter = root.GetComponentInChildren<EnemyWeaponShooter>(includeInactiveChildren);
            }
        }

        if (animatorDriver == null)
        {
            if (searchInParent)
                animatorDriver = animator.GetComponentInParent<EnemyAnimatorParameterDriver>();

            if (animatorDriver == null && searchInChildrenFromRoot)
            {
                Transform root = animator.transform.root;
                animatorDriver = root.GetComponentInChildren<EnemyAnimatorParameterDriver>(includeInactiveChildren);
            }
        }
    }

    private void AddLock(Animator animator)
    {
        if (lockedByThisState)
            return;

        if (lockController == null)
            CacheReferences(animator);

        if (lockController == null)
        {
            if (debugLog)
                Debug.LogWarning("[EnemyAnimatorShootLockBehaviour] Missing EnemyShootLockController.", animator);

            return;
        }

        lockController.AddShootLock();
        lockedByThisState = true;

        if (debugLog)
            Debug.Log("[EnemyAnimatorShootLockBehaviour] Shoot lock added.", animator);
    }

    private void RemoveLock(Animator animator)
    {
        if (!lockedByThisState)
            return;

        if (lockController == null)
            CacheReferences(animator);

        if (lockController != null)
            lockController.RemoveShootLock();

        lockedByThisState = false;

        if (debugLog)
            Debug.Log("[EnemyAnimatorShootLockBehaviour] Shoot lock removed.", animator);
    }

    private void ClearRuntimeState()
    {
        if (weaponShooter != null)
        {
            weaponShooter.externalShootLock = true;
            weaponShooter.ForceClearRuntimeState();
        }

        ClearAnimatorShooting();
    }

    private void ClearAnimatorShooting()
    {
        if (animatorDriver == null)
            return;

        animatorDriver.SetShooting(false);
        animatorDriver.SetKeepShooting(false);
    }
}
