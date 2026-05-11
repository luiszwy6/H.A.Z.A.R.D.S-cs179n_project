using UnityEngine;

[DisallowMultipleComponent]
public class LaserEndpoint : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerAimSettings aimSettings;
    [SerializeField] private Transform physicalAimingPoint;
    [SerializeField] private Transform rayStartPoint;

    [Header("Aim State")]
    [SerializeField] private bool useActiveViewAiming = true;
    [SerializeField] private bool usePlayerAimSettingsAiming = true;

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

    private void Reset()
    {
        Transform root = transform.root;

        if (playerMovement == null)
            playerMovement = root.GetComponent<PlayerMovement>();

        if (aimSettings == null)
            aimSettings = root.GetComponent<PlayerAimSettings>();
    }

    private void Awake()
    {
        Transform root = transform.root;

        if (playerMovement == null)
            playerMovement = root.GetComponent<PlayerMovement>();

        if (aimSettings == null)
            aimSettings = root.GetComponent<PlayerAimSettings>();

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

    private void CacheDefaultPosition()
    {
        defaultLocalPosition = transform.localPosition;
        defaultWorldPosition = transform.position;
        hasDefaultPosition = true;
    }

    private void UpdateEndpoint()
    {
        bool aiming = IsAimingNow();

        Vector3 targetPosition;

        if (aiming && physicalAimingPoint != null)
        {
            targetPosition = physicalAimingPoint.position;
            targetPosition = ResolveStoppedPosition(targetPosition);
            ApplyPosition(targetPosition);
            return;
        }

        targetPosition = GetDefaultWorldPosition();

        if (stopDefaultPositionByLayer)
            targetPosition = ResolveStoppedPosition(targetPosition);

        ApplyPosition(targetPosition);
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
}