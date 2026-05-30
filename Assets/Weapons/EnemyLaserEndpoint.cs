using UnityEngine;

[DisallowMultipleComponent]
public class EnemyLaserEndpoint : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private EnemySensor enemySensor;
    [SerializeField] private Animator enemyAnimator;
    [SerializeField] private Transform explicitTarget;
    [SerializeField] private Transform physicalAimingPoint;
    [SerializeField] private Transform rayStartPoint;

    [Header("Aim State")]
    [SerializeField] private bool alwaysAim = false;
    [SerializeField] private bool useAnimatorAiming = true;
    [SerializeField] private bool requireAnimatorAimingToFollowTarget = true;
    [SerializeField] private bool useSensorCanSeeTarget = true;
    [SerializeField] private bool requireSensorCanSeeTarget = false;
    [SerializeField] private string isAimingBoolName = "IsAiming";

    [Header("Target")]
    [SerializeField] private bool preferPhysicalAimingPoint = true;
    [SerializeField] private bool preferEnemySensorTarget = true;
    [SerializeField] private float targetHeightOffset = 1.3f;

    [Header("Default Position")]
    [SerializeField] private bool restoreDefaultLocalPositionWhenNotAiming = true;
    [SerializeField] private bool stopDefaultPositionByLayer = true;

    [Header("Follow")]
    [SerializeField] private bool smoothFollow = false;
    [SerializeField] private float followSpeed = 40f;

    [Header("Layer Stop")]
    [SerializeField] private bool stopByLayer = true;
    [SerializeField] private LayerMask stopLayers = 0;
    [SerializeField] private float stopSkin = 0.02f;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("Update")]
    [SerializeField] private bool updateInLateUpdate = true;

    private Vector3 defaultLocalPosition;
    private Vector3 defaultWorldPosition;
    private bool hasDefaultPosition;

    private int isAimingBoolHash;
    private bool hasAimingBoolHash;

    private void Reset()
    {
        Transform root = transform.root;

        if (enemySensor == null)
            enemySensor = root.GetComponent<EnemySensor>();

        if (enemyAnimator == null)
            enemyAnimator = root.GetComponentInChildren<Animator>();
    }

    private void Awake()
    {
        Transform root = transform.root;

        if (enemySensor == null)
            enemySensor = root.GetComponent<EnemySensor>();

        if (enemyAnimator == null)
            enemyAnimator = root.GetComponentInChildren<Animator>();

        CacheAnimatorHashes();
        CacheDefaultPosition();
    }

    private void OnEnable()
    {
        if (!hasDefaultPosition)
            CacheDefaultPosition();
    }

    private void Update()
    {
        if (updateInLateUpdate)
            return;

        UpdateEndpoint();
    }

    private void LateUpdate()
    {
        if (!updateInLateUpdate)
            return;

        UpdateEndpoint();
    }

    private void CacheAnimatorHashes()
    {
        hasAimingBoolHash = HasAnimatorBool(isAimingBoolName);

        if (hasAimingBoolHash)
            isAimingBoolHash = Animator.StringToHash(isAimingBoolName);
    }

    private void CacheDefaultPosition()
    {
        defaultLocalPosition = transform.localPosition;
        defaultWorldPosition = transform.position;
        hasDefaultPosition = true;
    }

    private void UpdateEndpoint()
    {
        Vector3 targetPosition;

        if (ShouldUseAimTarget() && TryResolveAimTargetPosition(out targetPosition))
        {
            targetPosition = ResolveStoppedPosition(targetPosition);
            ApplyPosition(targetPosition);
            return;
        }

        targetPosition = GetDefaultWorldPosition();

        if (stopDefaultPositionByLayer)
            targetPosition = ResolveStoppedPosition(targetPosition);

        ApplyPosition(targetPosition);
    }

    private bool ShouldUseAimTarget()
    {
        if (alwaysAim)
            return true;

        bool animatorAiming = IsAnimatorAiming();

        if (requireAnimatorAimingToFollowTarget && !animatorAiming)
            return false;

        bool sensorCanSeeTarget = false;

        if (useSensorCanSeeTarget || requireSensorCanSeeTarget)
        {
            if (enemySensor == null)
                enemySensor = transform.root.GetComponent<EnemySensor>();

            sensorCanSeeTarget = enemySensor != null && enemySensor.CanSeeTarget;

            if (requireSensorCanSeeTarget && !sensorCanSeeTarget)
                return false;

            if (useSensorCanSeeTarget && sensorCanSeeTarget)
                return true;
        }

        if (!useAnimatorAiming)
            return false;

        return animatorAiming;
    }

    private bool IsAnimatorAiming()
    {
        if (enemyAnimator == null || !hasAimingBoolHash)
            return false;

        return enemyAnimator.GetBool(isAimingBoolHash);
    }

    private bool TryResolveAimTargetPosition(out Vector3 targetPosition)
    {
        if (preferPhysicalAimingPoint && physicalAimingPoint != null)
        {
            targetPosition = physicalAimingPoint.position;
            return true;
        }

        Transform target = explicitTarget;

        if (target == null && preferEnemySensorTarget)
        {
            if (enemySensor == null)
                enemySensor = transform.root.GetComponent<EnemySensor>();

            if (enemySensor != null && enemySensor.Target != null)
                target = enemySensor.Target;
        }

        if (target != null)
        {
            targetPosition = target.position + Vector3.up * Mathf.Max(0f, targetHeightOffset);
            return true;
        }

        targetPosition = Vector3.zero;
        return false;
    }

    private Vector3 GetDefaultWorldPosition()
    {
        if (!hasDefaultPosition)
            CacheDefaultPosition();

        if (restoreDefaultLocalPositionWhenNotAiming && transform.parent != null)
            return transform.parent.TransformPoint(defaultLocalPosition);

        return defaultWorldPosition;
    }

    private Vector3 ResolveStoppedPosition(Vector3 targetPosition)
    {
        if (!stopByLayer)
            return targetPosition;

        if (stopLayers.value == 0)
            return targetPosition;

        Vector3 start = rayStartPoint != null ? rayStartPoint.position : transform.position;
        Vector3 dir = targetPosition - start;

        float distance = dir.magnitude;

        if (distance <= 0.0001f)
            return targetPosition;

        dir /= distance;

        if (Physics.Raycast(
                start,
                dir,
                out RaycastHit hit,
                distance,
                stopLayers,
                triggerInteraction))
        {
            return hit.point - dir * Mathf.Max(0f, stopSkin);
        }

        return targetPosition;
    }

    private void ApplyPosition(Vector3 position)
    {
        if (!smoothFollow)
        {
            transform.position = position;
            return;
        }

        float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, position, t);
    }

    private bool HasAnimatorBool(string parameterName)
    {
        if (enemyAnimator == null)
            return false;

        if (string.IsNullOrWhiteSpace(parameterName))
            return false;

        AnimatorControllerParameter[] parameters = enemyAnimator.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];

            if (parameter.type == AnimatorControllerParameterType.Bool &&
                parameter.name == parameterName)
            {
                return true;
            }
        }

        return false;
    }
}
