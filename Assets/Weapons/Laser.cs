using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(LineRenderer))]
public class Laser : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform endPoint;
    [SerializeField] private Transform aimingEndPoint;

    [Header("Aim State")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerAimSettings aimSettings;
    [SerializeField] private bool switchEndPointWhenAiming = true;
    [SerializeField] private bool useActiveViewAiming = true;
    [SerializeField] private bool usePlayerAimSettingsAiming = true;

    [Header("Settings")]
    [SerializeField] private bool visible = true;
    [SerializeField] private bool useWorldSpace = true;
    [SerializeField] private bool updateInLateUpdate = true;

    [Header("Fallback")]
    [SerializeField] private float fallbackDistance = 50f;
    [SerializeField] private Transform fallbackForwardSource;

    private void Reset()
    {
        lineRenderer = GetComponent<LineRenderer>();

        Transform root = transform.root;

        if (playerMovement == null)
            playerMovement = root.GetComponent<PlayerMovement>();

        if (aimSettings == null)
            aimSettings = root.GetComponent<PlayerAimSettings>();

        if (fallbackForwardSource == null)
            fallbackForwardSource = transform;

        SetupLineRenderer();
    }

    private void Awake()
    {
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();

        Transform root = transform.root;

        if (playerMovement == null)
            playerMovement = root.GetComponent<PlayerMovement>();

        if (aimSettings == null)
            aimSettings = root.GetComponent<PlayerAimSettings>();

        if (fallbackForwardSource == null)
            fallbackForwardSource = transform;

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
        if (switchEndPointWhenAiming && IsAimingNow() && aimingEndPoint != null)
            return aimingEndPoint;

        return endPoint;
    }

    private bool IsAimingNow()
    {
        if (useActiveViewAiming &&
            playerMovement != null &&
            playerMovement.ActiveView != null &&
            playerMovement.ActiveView.IsViewAiming)
        {
            return true;
        }

        if (usePlayerAimSettingsAiming &&
            aimSettings != null &&
            aimSettings.IsAiming)
        {
            return true;
        }

        return false;
    }
}