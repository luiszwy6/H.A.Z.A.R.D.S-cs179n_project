using UnityEngine;

public class EnemyAnimatorMoveLockBehaviour : StateMachineBehaviour
{
    [Header("Lock Timing")]
    [SerializeField] private bool lockOnEnter = true;
    [SerializeField] private bool unlockOnExit = true;

    private EnemyMovementLockController lockController;
    private bool lockedByThisState;

    public override void OnStateEnter(
        Animator animator,
        AnimatorStateInfo stateInfo,
        int layerIndex
    )
    {
        if (!lockOnEnter)
            return;

        lockController = animator.GetComponentInParent<EnemyMovementLockController>();

        if (lockController == null)
            return;

        lockController.AddMovementLock();
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
            lockController = animator.GetComponentInParent<EnemyMovementLockController>();

        if (lockController != null)
            lockController.RemoveMovementLock();

        lockedByThisState = false;
    }
}