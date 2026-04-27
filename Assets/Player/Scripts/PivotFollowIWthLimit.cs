using UnityEngine;

[DisallowMultipleComponent]
public class PivotFollowWithRadiusLimit : MonoBehaviour
{
    public enum VerticalLockMode
    {
        KeepInitialY,
        FixedWorldY,
        OrbitCenterPlusOffset
    }

    [Header("References")]
    [SerializeField] private Transform followTarget;   // e.g. AimingPoint
    [SerializeField] private Transform orbitCenter;    // e.g. Player

    [Header("Follow")]
    [SerializeField] private float followSpeed = 20f;

    [Header("Radius Limit")]
    [SerializeField] private float maxRadius = 3f;
    [SerializeField] private bool limitOnXZOnly = true;

    [Header("Vertical Lock")]
    [SerializeField] private bool lockVerticalPosition = true;
    [SerializeField] private VerticalLockMode verticalMode = VerticalLockMode.KeepInitialY;
    [SerializeField] private float fixedWorldY = 0f;
    [SerializeField] private float centerYOffset = 0f;

    [Header("When Follow Target Is Inactive")]
    [SerializeField] private bool returnToOrbitCenterWhenInactive = true;
    [SerializeField] private bool keepLastValidPositionWhenInactive = false;

    private Vector3 lastValidPosition;
    private bool hasLastValidPosition;
    private float initialY;

    private void Reset()
    {
        if (orbitCenter == null && transform.parent != null)
            orbitCenter = transform.parent;
    }

    private void Awake()
    {
        initialY = transform.position.y;
        lastValidPosition = transform.position;
        hasLastValidPosition = true;
    }

    private void LateUpdate()
    {
        if (orbitCenter == null)
            return;

        bool hasValidTarget = followTarget != null && followTarget.gameObject.activeInHierarchy;
        Vector3 desiredPosition;

        if (hasValidTarget)
        {
            desiredPosition = ClampToRadius(
                followTarget.position,
                orbitCenter.position,
                maxRadius,
                limitOnXZOnly
            );

            desiredPosition = ApplyVerticalLock(desiredPosition);

            lastValidPosition = desiredPosition;
            hasLastValidPosition = true;
        }
        else
        {
            if (returnToOrbitCenterWhenInactive)
            {
                desiredPosition = orbitCenter.position;
                desiredPosition = ApplyVerticalLock(desiredPosition);
            }
            else if (keepLastValidPositionWhenInactive && hasLastValidPosition)
            {
                desiredPosition = lastValidPosition;
                desiredPosition = ApplyVerticalLock(desiredPosition);
            }
            else
            {
                desiredPosition = transform.position;
                desiredPosition = ApplyVerticalLock(desiredPosition);
            }
        }

        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            1f - Mathf.Exp(-followSpeed * Time.deltaTime)
        );
    }

    private Vector3 ApplyVerticalLock(Vector3 position)
    {
        if (!lockVerticalPosition)
            return position;

        switch (verticalMode)
        {
            case VerticalLockMode.KeepInitialY:
                position.y = initialY;
                break;

            case VerticalLockMode.FixedWorldY:
                position.y = fixedWorldY;
                break;

            case VerticalLockMode.OrbitCenterPlusOffset:
                if (orbitCenter != null)
                    position.y = orbitCenter.position.y + centerYOffset;
                break;
        }

        return position;
    }

    private static Vector3 ClampToRadius(Vector3 targetPos, Vector3 centerPos, float radius, bool xzOnly)
    {
        radius = Mathf.Max(0f, radius);

        if (xzOnly)
        {
            Vector3 flatOffset = targetPos - centerPos;
            flatOffset.y = 0f;

            float dist = flatOffset.magnitude;
            if (dist > radius && dist > 0.0001f)
                flatOffset = flatOffset / dist * radius;

            Vector3 result = centerPos + flatOffset;
            result.y = targetPos.y;
            return result;
        }
        else
        {
            Vector3 offset = targetPos - centerPos;
            float dist = offset.magnitude;

            if (dist > radius && dist > 0.0001f)
                offset = offset / dist * radius;

            return centerPos + offset;
        }
    }

    public void SetFollowTarget(Transform newTarget)
    {
        followTarget = newTarget;
    }

    public void SetOrbitCenter(Transform newCenter)
    {
        orbitCenter = newCenter;
    }

    public void SnapNow()
    {
        if (orbitCenter == null)
            return;

        bool hasValidTarget = followTarget != null && followTarget.gameObject.activeInHierarchy;

        if (hasValidTarget)
        {
            Vector3 pos = ClampToRadius(
                followTarget.position,
                orbitCenter.position,
                maxRadius,
                limitOnXZOnly
            );

            pos = ApplyVerticalLock(pos);

            transform.position = pos;
            lastValidPosition = pos;
            hasLastValidPosition = true;
        }
        else
        {
            Vector3 pos = orbitCenter.position;
            pos = ApplyVerticalLock(pos);
            transform.position = pos;
        }
    }
}