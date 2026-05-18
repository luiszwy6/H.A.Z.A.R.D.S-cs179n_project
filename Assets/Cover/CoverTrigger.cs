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

    [SerializeField]
    private BoxCollider triggerCollider;

    private void Reset()
    {
        CacheCollider();
        EnsureTrigger();
    }

    private void OnValidate()
    {
        CacheCollider();
        EnsureTrigger();
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
    }
}
