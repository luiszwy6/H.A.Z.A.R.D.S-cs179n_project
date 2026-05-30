using UnityEngine;

public enum CoverType
{
    Low,
    High
}

[RequireComponent(typeof(BoxCollider))]
public class CoverTrigger : MonoBehaviour
{
    [Header("Cover Settings")]
    public CoverType coverType = CoverType.High;
    public Vector3 padding = Vector3.zero;

    [Header("Fit Target (Optional)")]
    public Renderer targetRenderer;

    [Header("Cover Surface")]
    [Tooltip("Real solid collider used to calculate cover snap position. This should not be a trigger.")]
    [SerializeField] private Collider surfaceCollider;

    [SerializeField] private float extraStandOff = 0.08f;
    [SerializeField] private float surfaceRayHeight = 1.0f;

    [Tooltip("True = character faces away from cover, so its back is against the cover.")]
    [SerializeField] private bool faceAwayFromCover = true;

    [Header("Reservation")]
    [SerializeField] private bool allowMultipleUsers = false;

    [SerializeField]
    private BoxCollider triggerCollider;

    private GameObject reservedBy;

    public Collider SurfaceCollider => surfaceCollider;
    public bool IsReserved => reservedBy != null;
    public GameObject ReservedBy => reservedBy;

    private void OnEnable()
    {
        CoverRegistry.Register(this);
    }

    private void OnDisable()
    {
        CoverRegistry.Unregister(this);
        reservedBy = null;
    }

    private void Reset()
    {
        CacheCollider();
        EnsureTrigger();
        TryFindSurfaceCollider();
    }

    private void OnValidate()
    {
        CacheCollider();
        EnsureTrigger();

        if (surfaceCollider == triggerCollider)
        {
            surfaceCollider = null;
        }

        TryFindSurfaceCollider();
    }

    private void CacheCollider()
    {
        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<BoxCollider>();
        }
    }

    private void EnsureTrigger()
    {
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void TryFindSurfaceCollider()
    {
        if (surfaceCollider != null && surfaceCollider != triggerCollider && !surfaceCollider.isTrigger)
        {
            return;
        }

        Collider[] parentColliders = GetComponentsInParent<Collider>(true);

        foreach (Collider col in parentColliders)
        {
            if (col == null)
            {
                continue;
            }

            if (col == triggerCollider)
            {
                continue;
            }

            if (col.isTrigger)
            {
                continue;
            }

            surfaceCollider = col;
            return;
        }

        Collider[] childColliders = GetComponentsInChildren<Collider>(true);

        foreach (Collider col in childColliders)
        {
            if (col == null)
            {
                continue;
            }

            if (col == triggerCollider)
            {
                continue;
            }

            if (col.isTrigger)
            {
                continue;
            }

            surfaceCollider = col;
            return;
        }
    }

    public bool TryAutoFitToRenderer()
    {
        CacheCollider();
        if (triggerCollider == null)
        {
            return false;
        }

        Renderer rendererToFit = targetRenderer != null ? targetRenderer : GetComponentInParent<Renderer>();
        if (rendererToFit == null)
        {
            return false;
        }

        Bounds worldBounds = rendererToFit.bounds;
        Vector3 localCenter = transform.InverseTransformPoint(worldBounds.center);
        Vector3 localSize = transform.InverseTransformVector(worldBounds.size);

        localSize = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
        localSize += padding;

        triggerCollider.center = localCenter;
        triggerCollider.size = localSize;
        return true;
    }

    public bool IsAvailableFor(GameObject user)
    {
        if (allowMultipleUsers)
        {
            return true;
        }

        return reservedBy == null || reservedBy == user;
    }

    public bool Reserve(GameObject user)
    {
        if (user == null)
        {
            return false;
        }

        if (!IsAvailableFor(user))
        {
            return false;
        }

        reservedBy = user;
        return true;
    }

    public void Release(GameObject user)
    {
        if (reservedBy == user)
        {
            reservedBy = null;
        }
    }

    public bool TryGetCoverPose(
        Vector3 requesterPosition,
        float requesterRadius,
        out Vector3 coverPosition,
        out Quaternion coverRotation)
    {
        coverPosition = requesterPosition;
        coverRotation = Quaternion.identity;

        TryFindSurfaceCollider();

        if (surfaceCollider != null)
        {
            return TryGetPoseFromSurfaceCollider(
                requesterPosition,
                requesterRadius,
                out coverPosition,
                out coverRotation
            );
        }

        Renderer rendererToUse = targetRenderer != null ? targetRenderer : GetComponentInParent<Renderer>();

        if (rendererToUse != null)
        {
            return TryGetPoseFromBounds(
                requesterPosition,
                requesterRadius,
                rendererToUse.bounds,
                out coverPosition,
                out coverRotation
            );
        }

        return false;
    }

    private bool TryGetPoseFromSurfaceCollider(
        Vector3 requesterPosition,
        float requesterRadius,
        out Vector3 coverPosition,
        out Quaternion coverRotation)
    {
        Vector3 queryPoint = requesterPosition + Vector3.up * surfaceRayHeight;

        Vector3 surfacePoint = surfaceCollider.ClosestPoint(queryPoint);

        Vector3 surfaceNormal = requesterPosition - surfacePoint;
        surfaceNormal.y = 0f;

        if (surfaceNormal.sqrMagnitude < 0.0001f)
        {
            surfaceNormal = requesterPosition - surfaceCollider.bounds.center;
            surfaceNormal.y = 0f;
        }

        if (surfaceNormal.sqrMagnitude < 0.0001f)
        {
            surfaceNormal = -transform.forward;
            surfaceNormal.y = 0f;
        }

        surfaceNormal.Normalize();

        float finalStandOff = Mathf.Max(0f, requesterRadius) + Mathf.Max(0f, extraStandOff);

        coverPosition = surfacePoint + surfaceNormal * finalStandOff;

        // Keep vertical position unchanged.
        coverPosition.y = requesterPosition.y;

        Vector3 facingDirection = faceAwayFromCover ? surfaceNormal : -surfaceNormal;

        if (facingDirection.sqrMagnitude < 0.0001f)
        {
            facingDirection = transform.forward;
        }

        coverRotation = Quaternion.LookRotation(facingDirection.normalized, Vector3.up);
        return true;
    }

    private bool TryGetPoseFromBounds(
        Vector3 requesterPosition,
        float requesterRadius,
        Bounds bounds,
        out Vector3 coverPosition,
        out Quaternion coverRotation)
    {
        Vector3 queryPoint = requesterPosition + Vector3.up * surfaceRayHeight;

        Vector3 surfacePoint = bounds.ClosestPoint(queryPoint);

        Vector3 surfaceNormal = requesterPosition - surfacePoint;
        surfaceNormal.y = 0f;

        if (surfaceNormal.sqrMagnitude < 0.0001f)
        {
            surfaceNormal = requesterPosition - bounds.center;
            surfaceNormal.y = 0f;
        }

        if (surfaceNormal.sqrMagnitude < 0.0001f)
        {
            surfaceNormal = -transform.forward;
            surfaceNormal.y = 0f;
        }

        surfaceNormal.Normalize();

        float finalStandOff = Mathf.Max(0f, requesterRadius) + Mathf.Max(0f, extraStandOff);

        coverPosition = surfacePoint + surfaceNormal * finalStandOff;

        // Keep vertical position unchanged.
        coverPosition.y = requesterPosition.y;

        Vector3 facingDirection = faceAwayFromCover ? surfaceNormal : -surfaceNormal;

        if (facingDirection.sqrMagnitude < 0.0001f)
        {
            facingDirection = transform.forward;
        }

        coverRotation = Quaternion.LookRotation(facingDirection.normalized, Vector3.up);
        return true;
    }

    private void OnDrawGizmos()
    {
        CacheCollider();
        if (triggerCollider == null)
        {
            return;
        }

        Gizmos.color = new Color(0.2f, 0.9f, 0.6f, 0.9f);
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(triggerCollider.center, triggerCollider.size);
        Gizmos.matrix = previousMatrix;

        TryFindSurfaceCollider();

        if (surfaceCollider != null)
        {
            Gizmos.color = new Color(0.1f, 0.45f, 1f, 0.8f);
            Gizmos.DrawWireCube(surfaceCollider.bounds.center, surfaceCollider.bounds.size);
        }
    }
}
