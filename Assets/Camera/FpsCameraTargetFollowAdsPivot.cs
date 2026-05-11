using UnityEngine;

[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
public class FpsCameraTargetFollowAdsPivot : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerFpsView fpsView;
    [SerializeField] private Transform adsPivot;

    [Header("Activation")]
    [SerializeField] private bool onlyWhileFpsViewEnabled = true;
    [SerializeField] private bool onlyWhileFpsAiming = true;

    [Header("Follow Position")]
    [SerializeField] private bool followPosition = true;
    [SerializeField] private bool smoothFollow = true;
    [Min(0f)] [SerializeField] private float followSpeed = 35f;

    [Header("Offset")]
    [SerializeField] private bool useWorldOffset = false;
    [SerializeField] private Vector3 worldOffset = Vector3.zero;

    [Header("Axes")]
    [SerializeField] private bool followX = true;
    [SerializeField] private bool followY = true;
    [SerializeField] private bool followZ = true;

    [Header("Debug")]
    [SerializeField] private bool logMissingRefs = false;

    private void Reset()
    {
        if (fpsView == null)
            fpsView = GetComponentInParent<PlayerFpsView>();
    }

    private void LateUpdate()
    {
        if (!ShouldFollow())
            return;

        Vector3 desiredPosition = adsPivot.position;

        if (useWorldOffset)
            desiredPosition += worldOffset;

        Vector3 finalPosition = transform.position;

        if (followX) finalPosition.x = desiredPosition.x;
        if (followY) finalPosition.y = desiredPosition.y;
        if (followZ) finalPosition.z = desiredPosition.z;

        if (!followPosition)
            return;

        if (smoothFollow)
        {
            transform.position = Vector3.Lerp(
                transform.position,
                finalPosition,
                1f - Mathf.Exp(-followSpeed * Time.deltaTime)
            );
        }
        else
        {
            transform.position = finalPosition;
        }
    }

    private bool ShouldFollow()
    {
        if (adsPivot == null)
        {
            if (logMissingRefs)
                Debug.LogWarning("[FpsCameraTargetFollowAdsPivot] adsPivot is null.", this);

            return false;
        }

        if (fpsView == null)
        {
            if (logMissingRefs)
                Debug.LogWarning("[FpsCameraTargetFollowAdsPivot] fpsView is null.", this);

            return false;
        }

        if (onlyWhileFpsViewEnabled && !fpsView.isActiveAndEnabled)
            return false;

        if (onlyWhileFpsAiming && !fpsView.IsViewAiming)
            return false;

        return true;
    }

    public void SetAdsPivot(Transform newAdsPivot)
    {
        adsPivot = newAdsPivot;
    }

    public void SetFpsView(PlayerFpsView newFpsView)
    {
        fpsView = newFpsView;
    }

    public void SnapNow()
    {
        if (adsPivot == null)
            return;

        Vector3 desiredPosition = adsPivot.position;

        if (useWorldOffset)
            desiredPosition += worldOffset;

        Vector3 finalPosition = transform.position;

        if (followX) finalPosition.x = desiredPosition.x;
        if (followY) finalPosition.y = desiredPosition.y;
        if (followZ) finalPosition.z = desiredPosition.z;

        transform.position = finalPosition;
    }
}