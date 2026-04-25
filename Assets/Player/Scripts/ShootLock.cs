using UnityEngine;

public class ShootLock : StateMachineBehaviour
{
    private PlayerShootSettings FindShootSettings(Animator animator)
    {
        PlayerShootSettings shoot = animator.GetComponent<PlayerShootSettings>();
        if (shoot != null) return shoot;

        shoot = animator.GetComponentInParent<PlayerShootSettings>();
        if (shoot != null) return shoot;

        shoot = animator.GetComponentInChildren<PlayerShootSettings>(true);
        if (shoot != null) return shoot;

        shoot = animator.transform.root.GetComponentInChildren<PlayerShootSettings>(true);
        return shoot;
    }

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        var shoot = FindShootSettings(animator);

        Debug.Log(
            $"[ShootLock ENTER] animator={animator.name}, layer={layerIndex}, shoot={(shoot != null ? shoot.name : "NULL")}",
            animator
        );

        if (shoot != null)
            shoot.externalShootLock = true;
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        var shoot = FindShootSettings(animator);

        Debug.Log(
            $"[ShootLock EXIT] animator={animator.name}, layer={layerIndex}, shoot={(shoot != null ? shoot.name : "NULL")}",
            animator
        );

        if (shoot != null)
            shoot.externalShootLock = false;
    }
}