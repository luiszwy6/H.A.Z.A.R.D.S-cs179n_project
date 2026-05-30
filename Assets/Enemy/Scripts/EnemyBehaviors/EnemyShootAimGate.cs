using Unity.Behavior;
using UnityEngine;
using UnityEngine.AI;

public static class EnemyShootAimGate
{
    public static void ApplyAimAndFacing(
        GameObject self,
        Vector3 targetPoint,
        bool setAnimatorAiming,
        bool aiming,
        bool faceTarget,
        float rotationSpeed,
        bool disableAgentRotation)
    {
        if (self == null)
            return;

        if (setAnimatorAiming)
        {
            EnemyAnimatorParameterDriver animatorDriver =
                self.GetComponent<EnemyAnimatorParameterDriver>();

            if (animatorDriver != null)
                animatorDriver.SetAiming(aiming);
        }

        NavMeshAgent agent = self.GetComponent<NavMeshAgent>();

        if (agent != null && disableAgentRotation)
            agent.updateRotation = false;

        if (!faceTarget)
            return;

        RotateTowardsTarget(self.transform, targetPoint, rotationSpeed);
    }

    public static bool CanShoot(
        GameObject self,
        Vector3 targetPoint,
        bool requireAnimatorAiming,
        bool requireFacingTarget,
        float angleTolerance,
        string isAimingBoolName,
        bool failIfAnimatorParameterMissing)
    {
        if (self == null)
            return false;

        if (requireAnimatorAiming &&
            !IsAnimatorAiming(self, isAimingBoolName, failIfAnimatorParameterMissing))
        {
            return false;
        }

        if (requireFacingTarget &&
            !IsFacingTarget(self.transform, targetPoint, angleTolerance))
        {
            return false;
        }

        return true;
    }

    public static bool ResolveBool(
        BlackboardVariable<bool> variable,
        bool defaultValue)
    {
        if (variable == null)
            return defaultValue;

        return variable.Value;
    }

    public static float ResolveFloat(
        BlackboardVariable<float> variable,
        float defaultValue)
    {
        if (variable == null)
            return defaultValue;

        return variable.Value;
    }

    private static void RotateTowardsTarget(
        Transform selfTransform,
        Vector3 targetPoint,
        float rotationSpeed)
    {
        if (selfTransform == null)
            return;

        Vector3 direction = targetPoint - selfTransform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.01f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);

        selfTransform.rotation = Quaternion.Slerp(
            selfTransform.rotation,
            targetRotation,
            Mathf.Max(0f, rotationSpeed) * Time.deltaTime
        );
    }

    private static bool IsFacingTarget(
        Transform selfTransform,
        Vector3 targetPoint,
        float angleTolerance)
    {
        if (selfTransform == null)
            return false;

        Vector3 direction = targetPoint - selfTransform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.01f)
            return true;

        float angle = Vector3.Angle(selfTransform.forward, direction.normalized);
        return angle <= Mathf.Max(0f, angleTolerance);
    }

    private static bool IsAnimatorAiming(
        GameObject self,
        string isAimingBoolName,
        bool failIfAnimatorParameterMissing)
    {
        if (self == null)
            return false;

        if (string.IsNullOrWhiteSpace(isAimingBoolName))
            return !failIfAnimatorParameterMissing;

        Animator animator = self.GetComponentInChildren<Animator>();

        if (animator == null)
            return !failIfAnimatorParameterMissing;

        AnimatorControllerParameter[] parameters = animator.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];

            if (parameter.type == AnimatorControllerParameterType.Bool &&
                parameter.name == isAimingBoolName)
            {
                return animator.GetBool(isAimingBoolName);
            }
        }

        return !failIfAnimatorParameterMissing;
    }
}
