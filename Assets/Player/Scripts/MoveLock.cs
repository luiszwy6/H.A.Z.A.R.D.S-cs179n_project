using UnityEngine;

public class MoveLock : StateMachineBehaviour
{
    private PlayerMovement playerMovement;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (playerMovement == null)
            playerMovement = animator.GetComponentInParent<PlayerMovement>();

        if (playerMovement != null)
            playerMovement.externalMovementLock = true;
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (playerMovement == null)
            playerMovement = animator.GetComponentInParent<PlayerMovement>();

        if (playerMovement != null)
            playerMovement.externalMovementLock = false;
    }
}