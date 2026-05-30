using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(LineRenderer))]
public class EnemyLaser : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform endPoint;
    [SerializeField] private Transform aimingEndPoint;
    [SerializeField] private Animator enemyAnimator;
    [SerializeField] private EnemySensor enemySensor;

    [Header("Aim State")]
    [SerializeField] private bool switchEndPointWhenAiming = true;
    [SerializeField] private bool useAnimatorAiming = true;
    [SerializeField] private bool useSensorCanSeeTarget = false;
    [SerializeField] private bool alwaysUseAimingEndPoint = false;
    [SerializeField] private string isAimingBoolName = "IsAiming";

    [Header("Settings")]
    [SerializeField] private bool visible = true;
    [SerializeField] private bool useWorldSpace = true;
    [SerializeField] private bool updateInLateUpdate = true;

    [Header("Fallback")]
    [SerializeField] private float fallbackDistance = 50f;
    [SerializeField] private Transform fallbackForwardSource;

    private int isAimingBoolHash;
    private bool hasAimingBoolHash;

    private void Reset()
    {
        lineRenderer = GetComponent<LineRenderer>();

        Transform root = transform.root;

        if (enemyAnimator == null)
            enemyAnimator = root.GetComponentInChildren<Animator>();

        if (enemySensor == null)
            enemySensor = root.GetComponent<EnemySensor>();

        if (fallbackForwardSource == null)
            fallbackForwardSource = transform;

        SetupLineRenderer();
    }

    private void Awake()
    {
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();

        Transform root = transform.root;

        if (enemyAnimator == null)
            enemyAnimator = root.GetComponentInChildren<Animator>();

        if (enemySensor == null)
            enemySensor = root.GetComponent<EnemySensor>();

        if (fallbackForwardSource == null)
            fallbackForwardSource = transform;

        CacheAnimatorHashes();
        SetupLineRenderer();
    }

    private void Update()
    {
        if (updateInLateUpdate)
            return;

        UpdateLaser();
    }

    private void LateUpdate()
    {
        if (!updateInLateUpdate)
            return;

        UpdateLaser();
    }

    public void SetVisible(bool value)
    {
        visible = value;

        if (lineRenderer != null)
            lineRenderer.enabled = visible;
    }

    public void SetStartPoint(Transform point)
    {
        startPoint = point;
    }

    public void SetEndPoint(Transform point)
    {
        endPoint = point;
    }

    public void SetAimingEndPoint(Transform point)
    {
        aimingEndPoint = point;
    }

    private void CacheAnimatorHashes()
    {
        hasAimingBoolHash = HasAnimatorBool(isAimingBoolName);

        if (hasAimingBoolHash)
            isAimingBoolHash = Animator.StringToHash(isAimingBoolName);
    }

    private void SetupLineRenderer()
    {
        if (lineRenderer == null)
            return;

        lineRenderer.positionCount = 2;
        lineRenderer.useWorldSpace = useWorldSpace;
        lineRenderer.enabled = visible;
    }

    private void UpdateLaser()
    {
        if (lineRenderer == null)
            return;

        lineRenderer.enabled = visible;

        if (!visible)
            return;

        Vector3 start = startPoint != null ? startPoint.position : transform.position;
        Vector3 end = ResolveEndPoint(start);

        lineRenderer.useWorldSpace = useWorldSpace;
        lineRenderer.positionCount = 2;

        if (useWorldSpace)
        {
            lineRenderer.SetPosition(0, start);
            lineRenderer.SetPosition(1, end);
        }
        else
        {
            lineRenderer.SetPosition(0, transform.InverseTransformPoint(start));
            lineRenderer.SetPosition(1, transform.InverseTransformPoint(end));
        }
    }

    private Vector3 ResolveEndPoint(Vector3 start)
    {
        Transform resolvedEndPoint = GetCurrentEndPoint();

        if (resolvedEndPoint != null)
            return resolvedEndPoint.position;

        Transform forwardSource = fallbackForwardSource != null
            ? fallbackForwardSource
            : transform;

        Vector3 forward = forwardSource.forward;

        if (forward.sqrMagnitude <= 0.0001f)
            forward = Vector3.forward;

        return start + forward.normalized * fallbackDistance;
    }

    private Transform GetCurrentEndPoint()
    {
        if ((alwaysUseAimingEndPoint ||
             switchEndPointWhenAiming && IsAimingNow()) &&
            aimingEndPoint != null)
        {
            return aimingEndPoint;
        }

        return endPoint;
    }

    private bool IsAimingNow()
    {
        if (useSensorCanSeeTarget)
        {
            if (enemySensor == null)
                enemySensor = transform.root.GetComponent<EnemySensor>();

            if (enemySensor != null && enemySensor.CanSeeTarget)
                return true;
        }

        if (!useAnimatorAiming || enemyAnimator == null || !hasAimingBoolHash)
            return false;

        return enemyAnimator.GetBool(isAimingBoolHash);
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
