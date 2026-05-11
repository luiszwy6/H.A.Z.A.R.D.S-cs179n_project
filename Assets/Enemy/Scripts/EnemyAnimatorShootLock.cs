using UnityEngine;
using Unity.Behavior;

public class EnemyAnimatorCanShootLock : StateMachineBehaviour
{
    [Header("Behavior Blackboard")]
    [SerializeField] private string canShootVariableName = "CanShoot";

    [Header("Timing")]
    [SerializeField] private bool setFalseOnEnter = true;
    [SerializeField] private bool keepFalseWhileInState = true;
    [SerializeField] private bool restoreOnExit = true;

    [Header("Restore")]
    [SerializeField] private bool valueOnExit = true;

    [Header("Search")]
    [SerializeField] private bool searchInParents = true;
    [SerializeField] private bool searchFromRootChildren = true;
    [SerializeField] private bool includeInactiveChildren = true;

    [Header("Optional Runtime Clear")]
    [SerializeField] private bool clearAnimatorShootingOnEnter = true;
    [SerializeField] private bool clearAnimatorShootingWhileInState = true;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;

    private BehaviorGraphAgent behaviorAgent;
    private EnemyAnimatorParameterDriver animatorDriver;

    public override void OnStateEnter(
        Animator animator,
        AnimatorStateInfo stateInfo,
        int layerIndex)
    {
        CacheReferences(animator);

        if (setFalseOnEnter)
            SetCanShoot(animator, false);

        if (clearAnimatorShootingOnEnter)
            ClearAnimatorShooting();

        if (debugLog)
        {
            Debug.Log(
                $"[EnemyAnimatorCanShootLock] Enter state. Set {canShootVariableName}=false",
                animator
            );
        }
    }

    public override void OnStateUpdate(
        Animator animator,
        AnimatorStateInfo stateInfo,
        int layerIndex)
    {
        if (keepFalseWhileInState)
            SetCanShoot(animator, false);

        if (clearAnimatorShootingWhileInState)
            ClearAnimatorShooting();
    }

    public override void OnStateExit(
        Animator animator,
        AnimatorStateInfo stateInfo,
        int layerIndex)
    {
        if (restoreOnExit)
            SetCanShoot(animator, valueOnExit);

        if (debugLog)
        {
            Debug.Log(
                $"[EnemyAnimatorCanShootLock] Exit state. Set {canShootVariableName}={valueOnExit}",
                animator
            );
        }
    }

    private void CacheReferences(Animator animator)
    {
        if (animator == null)
            return;

        GameObject animatorObject = animator.gameObject;

        if (behaviorAgent == null)
        {
            if (searchInParents)
                behaviorAgent = animatorObject.GetComponentInParent<BehaviorGraphAgent>();

            if (behaviorAgent == null && searchFromRootChildren)
            {
                behaviorAgent =
                    animator.transform.root.GetComponentInChildren<BehaviorGraphAgent>(
                        includeInactiveChildren
                    );
            }
        }

        if (animatorDriver == null)
        {
            if (searchInParents)
                animatorDriver = animatorObject.GetComponentInParent<EnemyAnimatorParameterDriver>();

            if (animatorDriver == null && searchFromRootChildren)
            {
                animatorDriver =
                    animator.transform.root.GetComponentInChildren<EnemyAnimatorParameterDriver>(
                        includeInactiveChildren
                    );
            }
        }
    }

    private void SetCanShoot(Animator animator, bool value)
    {
        if (behaviorAgent == null)
            CacheReferences(animator);

        if (behaviorAgent == null)
        {
            if (debugLog)
                Debug.LogWarning("[EnemyAnimatorCanShootLock] Missing BehaviorGraphAgent.", animator);

            return;
        }

        if (string.IsNullOrWhiteSpace(canShootVariableName))
            return;

        bool success = behaviorAgent.SetVariableValue(canShootVariableName, value);

        if (!success && debugLog)
        {
            Debug.LogWarning(
                $"[EnemyAnimatorCanShootLock] Failed to set Blackboard bool '{canShootVariableName}' = {value}. " +
                "Check that the variable exists and is Bool.",
                animator
            );
        }
    }

    private void ClearAnimatorShooting()
    {
        if (animatorDriver == null)
            return;

        animatorDriver.SetShooting(false);
        animatorDriver.SetKeepShooting(false);
    }
}