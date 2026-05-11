using UnityEngine;

[DisallowMultipleComponent]
public class HorizontalFollowTarget : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform target; // AimingPoint

    [Header("Follow")]
    [SerializeField] private bool smoothFollow = true;
    [Min(0f)] [SerializeField] private float followSpeed = 25f;

    [Header("Ground Based Height")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float heightAboveGround = 15f;
    [SerializeField] private float rayStartHeight = 100f;
    [SerializeField] private float rayDistance = 300f;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("Fallback")]
    [SerializeField] private bool keepCurrentYIfNoGroundHit = true;
    [SerializeField] private float fallbackWorldY = 15f;

    [Header("Options")]
    [SerializeField] private bool snapOnEnable = true;
    [SerializeField] private bool drawDebugRay = false;

    private void OnEnable()
    {
        if (snapOnEnable)
            SnapNow();
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 desiredPosition = transform.position;

        desiredPosition.x = target.position.x;
        desiredPosition.z = target.position.z;
        desiredPosition.y = ResolveGroundBasedY(desiredPosition.x, desiredPosition.z);

        if (smoothFollow)
        {
            transform.position = Vector3.Lerp(
                transform.position,
                desiredPosition,
                1f - Mathf.Exp(-followSpeed * Time.deltaTime)
            );
        }
        else
        {
            transform.position = desiredPosition;
        }
    }

    private float ResolveGroundBasedY(float x, float z)
    {
        Vector3 rayOrigin = new Vector3(
            x,
            transform.position.y + rayStartHeight,
            z
        );

        if (Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out RaycastHit hit,
                rayDistance,
                groundMask,
                triggerInteraction))
        {
            if (drawDebugRay)
                Debug.DrawLine(rayOrigin, hit.point, Color.green, 0f, false);

            return hit.point.y + heightAboveGround;
        }

        if (drawDebugRay)
            Debug.DrawRay(rayOrigin, Vector3.down * rayDistance, Color.red, 0f, false);

        if (keepCurrentYIfNoGroundHit)
            return transform.position.y;

        return fallbackWorldY;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    public void SetHeightAboveGround(float height)
    {
        heightAboveGround = height;
    }

    public void SetGroundMask(LayerMask newGroundMask)
    {
        groundMask = newGroundMask;
    }

    public void SnapNow()
    {
        if (target == null)
            return;

        Vector3 pos = transform.position;

        pos.x = target.position.x;
        pos.z = target.position.z;
        pos.y = ResolveGroundBasedY(pos.x, pos.z);

        transform.position = pos;
    }
}