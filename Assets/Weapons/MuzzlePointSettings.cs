using UnityEngine;

[DisallowMultipleComponent]
public class MuzzlePointSettings : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform muzzlePoint;
    [SerializeField] private PlayerCrossHairSettings crosshairSettings;

    [Header("Raycast (Bullet Trajectory Debug)")]
    [SerializeField] private LayerMask hitLayers = ~0;
    [Min(0.01f)] [SerializeField] private float rangeOfProjectile = 50f;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("Debug Draw")]
    [Min(0.01f)] [SerializeField] private float debugDrawDuration = 0.35f;
    [Min(0.0001f)] [SerializeField] private float gizmoSphereRadius = 0.06f;
    [SerializeField] private Color bulletRayColor = Color.red;
    [SerializeField] private Color aimPointColor = Color.yellow;
    [SerializeField] private bool drawInGameView = true;
    [SerializeField] private bool drawInSceneGizmos = true;

    public bool HasHit { get; private set; }
    public RaycastHit LastHit { get; private set; }
    public Vector3 AimPoint { get; private set; }
    public Ray LastRay { get; private set; }

    private float _debugTimer;

    private void Reset()
    {
        if (muzzlePoint == null) muzzlePoint = transform;
    }

    private void Update()
    {
        if (_debugTimer > 0f)
        {
            _debugTimer -= Time.deltaTime;

            if (drawInGameView)
                DrawDebugLineGame();
        }
    }

    public void RequestDebugDraw()
    {
        ComputeShotRaycast();
        _debugTimer = debugDrawDuration;

        if (drawInGameView)
            DrawDebugLineGame();
    }

    private void ComputeShotRaycast()
    {
        HasHit = false;
        LastHit = default;

        if (muzzlePoint == null)
            return;

        AimPoint = ResolveAimPoint();

        Vector3 origin = muzzlePoint.position;
        Vector3 toAim = AimPoint - origin;
        Vector3 dir = toAim.sqrMagnitude > 0.0001f ? toAim.normalized : muzzlePoint.forward;

        LastRay = new Ray(origin, dir);

        if (Physics.Raycast(LastRay, out RaycastHit hit, rangeOfProjectile, hitLayers, triggerInteraction))
        {
            HasHit = true;
            LastHit = hit;
        }
    }

    private Vector3 ResolveAimPoint()
    {
        if (crosshairSettings != null)
            return crosshairSettings.AimPointClamped;

        if (muzzlePoint != null)
            return muzzlePoint.position + muzzlePoint.forward * rangeOfProjectile;

        return Vector3.zero;
    }

    private void DrawDebugLineGame()
    {
        Vector3 a = LastRay.origin;
        Vector3 b = HasHit ? LastHit.point : AimPoint;

        Debug.DrawLine(a, b, bulletRayColor, 0f, false);
        Debug.DrawLine(AimPoint, AimPoint + Vector3.up * 0.15f, aimPointColor, 0f, false);
    }

    private void OnDrawGizmos()
    {
        if (!drawInSceneGizmos) return;

        if (!Application.isPlaying)
            ComputeShotRaycast();

        if (Application.isPlaying && _debugTimer <= 0f)
            return;

        Gizmos.color = bulletRayColor;

        Vector3 a = LastRay.origin;
        Vector3 b = HasHit ? LastHit.point : AimPoint;

        Gizmos.DrawLine(a, b);
        Gizmos.DrawWireSphere(b, gizmoSphereRadius);

        Gizmos.color = aimPointColor;
        Gizmos.DrawWireSphere(AimPoint, gizmoSphereRadius * 0.9f);
    }

    public void SetMuzzle(Transform t) => muzzlePoint = t;
    public void SetCrosshairSettings(PlayerCrossHairSettings s) => crosshairSettings = s;
}