using UnityEngine;

public class EnemyAnimatorShootLockBehaviour : StateMachineBehaviour
{
    [Header("Lock Timing")]
    [SerializeField] private bool lockOnEnter = true;
    [SerializeField] private bool unlockOnExit = true;

    private EnemyShootLockController lockController;
    private bool lockedByThisState;

    public override void OnStateEnter(
        Animator animator,
        AnimatorStateInfo stateInfo,
        int layerIndex
    )
    {
        if (!lockOnEnter)
            return;

        lockController = animator.GetComponentInParent<EnemyShootLockController>();

        if (lockController == null)
            return;

        lockController.AddShootLock();
        lockedByThisState = true;
    }

    public override void OnStateExit(
        Animator animator,
        AnimatorStateInfo stateInfo,
        int layerIndex
    )
    {
        if (!unlockOnExit)
            return;

        if (!lockedByThisState)
            return;

        if (lockController == null)
            lockController = animator.GetComponentInParent<EnemyShootLockController>();

        if (lockController != null)
            lockController.RemoveShootLock();

        lockedByThisState = false;
    }
}