using UnityEngine;

[DisallowMultipleComponent]
public class BulletProjectile : MonoBehaviour
{
    [Header("Projectile")]
    public float speed = 120f;
    public float maxDistance = 50f;
    public float radius = 0.04f;

    [Header("Hit Query")]
    public LayerMask hitMask = ~0;
    public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

    [Header("Behavior")]
    public bool destroyOnAnyHit = true;

    [Header("Debug")]
    public bool drawDebugLine = false;
    public Color debugColor = Color.red;

    [Header("Gizmos / Debug View")]
    public bool drawGizmos = true;
    public bool drawOnlyWhenSelected = false;
    public bool drawSweepSegment = true;
    public Color gizmoSphereColor = Color.red;
    public Color gizmoSweepColor = Color.yellow;

    private Vector3 _dir;
    private float _traveled = 0f;
    private bool _inited = false;

    private Vector3 _prevPosForGizmo;
    private bool _hasPrevPosForGizmo = false;

    public void Init(
        Vector3 origin,
        Vector3 direction,
        float speed,
        float maxDistance,
        float radius,
        LayerMask hitMask,
        QueryTriggerInteraction triggerInteraction)
    {
        transform.position = origin;

        _dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        this.speed = Mathf.Max(0.01f, speed);
        this.maxDistance = Mathf.Max(0.01f, maxDistance);
        this.radius = Mathf.Max(0.001f, radius);

        this.hitMask = hitMask;
        this.triggerInteraction = triggerInteraction;

        _traveled = 0f;
        _inited = true;

        transform.rotation = Quaternion.LookRotation(_dir, Vector3.up);

        _prevPosForGizmo = transform.position;
        _hasPrevPosForGizmo = true;
    }

    private void Update()
    {
        if (!_inited) return;

        float step = speed * Time.deltaTime;
        if (step <= 0f) return;

        Vector3 from = transform.position;
        Vector3 to = from + _dir * step;

        _prevPosForGizmo = from;
        _hasPrevPosForGizmo = true;

        if (Physics.SphereCast(from, radius, _dir, out RaycastHit hit, step, hitMask, triggerInteraction))
        {
            transform.position = hit.point;

            if (destroyOnAnyHit)
            {
                Destroy(gameObject);
                return;
            }
        }

        transform.position = to;
        _traveled += step;

        if (drawDebugLine)
        {
            Debug.DrawLine(from, to, debugColor, 0f, false);
        }

        if (_traveled >= maxDistance)
        {
            Destroy(gameObject);
        }
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos || drawOnlyWhenSelected) return;
        DrawProjectileGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos || !drawOnlyWhenSelected) return;
        DrawProjectileGizmos();
    }

    private void DrawProjectileGizmos()
    {
        float r = Mathf.Max(0.001f, radius);

        Gizmos.color = gizmoSphereColor;
        Gizmos.DrawWireSphere(transform.position, r);

        if (!drawSweepSegment || !_hasPrevPosForGizmo) return;

        Gizmos.color = gizmoSweepColor;
        Gizmos.DrawLine(_prevPosForGizmo, transform.position);
        Gizmos.DrawWireSphere(_prevPosForGizmo, r);
        Gizmos.DrawWireSphere(transform.position, r);
    }
}