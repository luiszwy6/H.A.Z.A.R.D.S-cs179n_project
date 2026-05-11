using UnityEngine;

[DisallowMultipleComponent]
public class MuzzlePointSettings : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform muzzlePoint;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerAimSettings aimSettings;
    [SerializeField] private PlayerCrossHairSettings PlayerCrossHairSettings;

    [Header("Fallback")]
    [SerializeField] private Transform fallbackForwardSource;
    [Min(0.01f)] [SerializeField] private float fallbackDistance = 50f;

    [Header("Debug")]
    [SerializeField] private bool drawDebugRay = true;
    [SerializeField] private float debugDrawDuration = 0.08f;
    [SerializeField] private Color debugRayColor = Color.red;

    [Header("Gizmos")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private bool drawOnlyWhenSelected = false;
    [SerializeField] private Color gizmoRayColor = Color.red;
    [SerializeField] private Color gizmoAimPointColor = Color.yellow;
    [SerializeField] private float gizmoAimPointRadius = 0.08f;

    private bool debugDrawRequested;
    private float debugDrawTimer;

    public Vector3 AimPoint { get; private set; }
    public Ray LastRay { get; private set; }

    private void Reset()
    {
        Transform root = transform.root;

        if (muzzlePoint == null)
            muzzlePoint = transform;

        if (playerMovement == null)
            playerMovement = root.GetComponent<PlayerMovement>();

        if (aimSettings == null)
            aimSettings = root.GetComponent<PlayerAimSettings>();

        if (PlayerCrossHairSettings == null)
            PlayerCrossHairSettings = GetComponent<PlayerCrossHairSettings>();

        if (fallbackForwardSource == null)
            fallbackForwardSource = root;
    }

    private void Awake()
    {
        Transform root = transform.root;

        if (muzzlePoint == null)
            muzzlePoint = transform;

        if (playerMovement == null)
            playerMovement = root.GetComponent<PlayerMovement>();

        if (aimSettings == null)
            aimSettings = root.GetComponent<PlayerAimSettings>();

        if (PlayerCrossHairSettings == null)
            PlayerCrossHairSettings = GetComponent<PlayerCrossHairSettings>();

        if (fallbackForwardSource == null)
            fallbackForwardSource = root;

        UpdateMuzzleRay();
    }

    private void LateUpdate()
    {
        UpdateMuzzleRay();

        if (debugDrawRequested)
        {
            debugDrawTimer -= Time.deltaTime;

            if (drawDebugRay)
                Debug.DrawRay(LastRay.origin, LastRay.direction * fallbackDistance, debugRayColor, 0f, false);

            if (debugDrawTimer <= 0f)
                debugDrawRequested = false;
        }
    }

    public void RequestDebugDraw()
    {
        debugDrawRequested = true;
        debugDrawTimer = Mathf.Max(0.01f, debugDrawDuration);
    }

    private void UpdateMuzzleRay()
    {
        Vector3 origin = muzzlePoint != null ? muzzlePoint.position : transform.position;

        Vector3 targetPoint = ResolveAimPoint(origin);

        Vector3 dir = targetPoint - origin;

        if (dir.sqrMagnitude <= 0.0001f)
            dir = GetFallbackForward();

        dir.Normalize();

        AimPoint = targetPoint;
        LastRay = new Ray(origin, dir);
    }

    private Vector3 ResolveAimPoint(Vector3 origin)
    {
        if (playerMovement != null &&
            playerMovement.ActiveView != null &&
            playerMovement.ActiveView.TryGetMuzzleAimPoint(out Vector3 viewAimPoint))
        {
            return viewAimPoint;
        }

        if (aimSettings != null && aimSettings.IsAiming)
        {
            if (PlayerCrossHairSettings != null)
                return PlayerCrossHairSettings.AimPointClamped;

            return aimSettings.AimPointClamped;
        }

        return origin + GetFallbackForward() * fallbackDistance;
    }

    private Vector3 GetFallbackForward()
    {
        Transform source = fallbackForwardSource != null ? fallbackForwardSource : transform;

        Vector3 forward = source.forward;

        if (forward.sqrMagnitude <= 0.0001f)
            forward = Vector3.forward;

        return forward.normalized;
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos || drawOnlyWhenSelected)
            return;

        DrawMuzzleGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos || !drawOnlyWhenSelected)
            return;

        DrawMuzzleGizmos();
    }

    private void DrawMuzzleGizmos()
    {
        Vector3 origin = muzzlePoint != null ? muzzlePoint.position : transform.position;

        Ray rayToDraw = Application.isPlaying
            ? LastRay
            : new Ray(origin, transform.forward);

        Vector3 aimPointToDraw = Application.isPlaying
            ? AimPoint
            : origin + transform.forward * fallbackDistance;

        Gizmos.color = gizmoRayColor;
        Gizmos.DrawLine(rayToDraw.origin, aimPointToDraw);

        Gizmos.color = gizmoAimPointColor;
        Gizmos.DrawWireSphere(aimPointToDraw, gizmoAimPointRadius);
    }
}