using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class PlayerCrossHairSettings : MonoBehaviour
{
    [Header("References")]
    public PlayerAimSettings aimSettings;
    public Transform physicalAimPoint;
    public Transform playerEyePoint;

    [Header("Top-Down Aim Sampling")]
    public LayerMask mouseAimLayers = ~0;
    public LayerMask mouseGroundLayers = ~0;
    public float mouseRayMaxDistance = 200f;
    public float mouseGroundHeightOffset = 0f;
    [SerializeField] private QueryTriggerInteraction mouseRayTriggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("Aim Pivot - Optional")]
    public Transform aimPivot;
    public float aimPivotHeight = 0.0f;
    public float pivotFollowSpeed = 20f;

    [Header("Stop By Layer - Optional")]
    public bool stopByLayer = true;
    public LayerMask stopLayers = 0;
    public float stopRayStartHeight = 0.6f;
    public float stopRayEndHeight = 0.1f;
    public float stopPadding = 0.05f;
    [SerializeField] private QueryTriggerInteraction stopRayTriggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("Visibility")]
    public bool forceHideCrosshair = false;
    public bool hideWhenNotAiming = true;

    [Header("Recoil Return")]
    public float recoilReturnSpeed = 18f;

    [Header("Aim Shot Recoil")]
    public float aimShotRecoilUpMetersPerShot = 0.12f;
    public float aimShotRecoilBackMetersPerShot = 0.00f;

    [Header("Quick Shot Recoil")]
    public float quickShotRecoilUpMetersPerShot = 0.18f;
    public float quickShotRecoilBackMetersPerShot = 0.02f;

    [Header("Gizmos")]
    public bool drawAimRaysGizmo = true;
    public Color gizmoPlayerToAimColor = new Color(1f, 0.35f, 0.1f, 0.9f);
    public Color gizmoScreenRayColor = new Color(0.1f, 1f, 0.2f, 0.9f);
    public bool gizmoOnlyWhenAiming = true;

    public Vector3 AimPointClamped { get; private set; }
    public Vector3 AimWorldDir { get; private set; }
    public bool HasMouseAimPoint { get; private set; }
    public Vector3 MouseAimPoint { get; private set; }

    private Vector3 recoilOffsetWorld;

    private Transform lastActor;
    private bool lastIsAiming;

    private bool hasLastScreenRay;
    private Vector3 lastScreenRayOrigin;
    private Vector3 lastScreenRayDir;
    private float lastScreenRayDrawDist;

    private void Reset()
    {
        if (aimSettings == null)
            aimSettings = transform.root.GetComponent<PlayerAimSettings>();
    }

    public void Tick(
        Transform actor,
        bool isCrouching,
        bool isAiming,
        Vector2 lookInput,
        bool usingMouseScheme,
        Camera aimCamera,
        Transform cameraTransform
    )
    {
        lastActor = actor;
        lastIsAiming = isAiming;

        recoilOffsetWorld = Vector3.Lerp(
            recoilOffsetWorld,
            Vector3.zero,
            1f - Mathf.Exp(-recoilReturnSpeed * Time.deltaTime)
        );

        AimWorldDir = Vector3.zero;
        HasMouseAimPoint = false;
        MouseAimPoint = Vector3.zero;

        hasLastScreenRay = false;
        lastScreenRayOrigin = Vector3.zero;
        lastScreenRayDir = Vector3.forward;
        lastScreenRayDrawDist = 0f;

        if (!isAiming)
        {
            AimPointClamped = actor.position;
            UpdatePivot(actor, false, Time.deltaTime);
            UpdateCrosshairVisual(false);
            return;
        }

        Vector3 desiredAimPoint = actor.position + actor.forward * mouseRayMaxDistance;

        if (usingMouseScheme && aimCamera != null && Mouse.current != null)
        {
            Vector2 screenPos = Mouse.current.position.ReadValue();
            Ray ray = aimCamera.ScreenPointToRay(screenPos);

            hasLastScreenRay = true;
            lastScreenRayOrigin = ray.origin;
            lastScreenRayDir = ray.direction.normalized;

            if (mouseAimLayers.value != 0 &&
                Physics.Raycast(ray, out RaycastHit hit, mouseRayMaxDistance, mouseAimLayers, mouseRayTriggerInteraction))
            {
                MouseAimPoint = hit.point;
                HasMouseAimPoint = true;
                desiredAimPoint = hit.point;
                lastScreenRayDrawDist = hit.distance;
            }
            else if (mouseGroundLayers.value != 0 &&
                     Physics.Raycast(ray, out RaycastHit groundHit, mouseRayMaxDistance, mouseGroundLayers, mouseRayTriggerInteraction))
            {
                Vector3 groundPoint = groundHit.point;
                groundPoint.y += mouseGroundHeightOffset;

                MouseAimPoint = groundPoint;
                HasMouseAimPoint = true;
                desiredAimPoint = groundPoint;
                lastScreenRayDrawDist = groundHit.distance;
            }
            else
            {
                desiredAimPoint = ray.origin + ray.direction * mouseRayMaxDistance;
                lastScreenRayDrawDist = mouseRayMaxDistance;
            }
        }

        Vector3 aimPoint = StopAimPointByLayer(actor, desiredAimPoint);
        AimPointClamped = aimPoint + recoilOffsetWorld;

        Vector3 dirToAim = AimPointClamped - actor.position;
        dirToAim.y = 0f;

        if (dirToAim.sqrMagnitude > 0.001f)
            AimWorldDir = dirToAim.normalized;

        UpdatePivot(actor, true, Time.deltaTime);
        UpdateCrosshairVisual(true);
    }

    private void UpdatePivot(Transform actor, bool isAiming, float dt)
    {
        if (aimPivot == null)
            return;

        Vector3 targetPos = isAiming ? AimPointClamped : actor.position;
        targetPos.y += aimPivotHeight;

        float t = 1f - Mathf.Exp(-pivotFollowSpeed * Mathf.Max(0.0001f, dt));
        aimPivot.position = Vector3.Lerp(aimPivot.position, targetPos, t);
    }

    private void UpdateCrosshairVisual(bool isAiming)
    {
        if (physicalAimPoint == null)
            return;

        bool visible = !forceHideCrosshair && (!hideWhenNotAiming || isAiming);

        if (physicalAimPoint.gameObject.activeSelf != visible)
            physicalAimPoint.gameObject.SetActive(visible);

        if (!visible)
            return;

        physicalAimPoint.position = AimPointClamped;
    }

    private Vector3 StopAimPointByLayer(Transform actor, Vector3 aimPoint)
    {
        if (!stopByLayer || stopLayers.value == 0)
            return aimPoint;

        Vector3 start = playerEyePoint != null
            ? playerEyePoint.position
            : actor.position + Vector3.up * stopRayStartHeight;

        Vector3 end = aimPoint;
        end.y = actor.position.y + stopRayEndHeight;

        Vector3 dir = end - start;
        float dist = dir.magnitude;

        if (dist < 0.001f)
            return aimPoint;

        dir /= dist;

        if (Physics.Raycast(start, dir, out RaycastHit hit, dist, stopLayers, stopRayTriggerInteraction))
        {
            Vector3 p = hit.point - dir * Mathf.Max(0f, stopPadding);
            p.y = aimPoint.y;
            return p;
        }

        return aimPoint;
    }

    public void AddAimShotRecoil()
    {
        AddRecoil(aimShotRecoilUpMetersPerShot, aimShotRecoilBackMetersPerShot);
    }

    public void AddQuickShotRecoil()
    {
        AddRecoil(quickShotRecoilUpMetersPerShot, quickShotRecoilBackMetersPerShot);
    }

    public void AddRecoil()
    {
        AddAimShotRecoil();
    }

    public void AddRecoil(float upMeters, float backMeters)
    {
        Vector3 up = Vector3.up * Mathf.Max(0f, upMeters);

        Vector3 back = Vector3.zero;

        if (lastActor != null)
            back = -lastActor.forward * Mathf.Max(0f, backMeters);

        recoilOffsetWorld += up + back;
    }

    private void OnDrawGizmos()
    {
        if (!drawAimRaysGizmo)
            return;

        if (gizmoOnlyWhenAiming && Application.isPlaying && !lastIsAiming)
            return;

        if (lastActor == null)
            return;

        Gizmos.color = gizmoPlayerToAimColor;

        Vector3 start = playerEyePoint != null
            ? playerEyePoint.position
            : lastActor.position + Vector3.up * stopRayStartHeight;

        Gizmos.DrawLine(start, AimPointClamped);
        Gizmos.DrawWireSphere(AimPointClamped, 0.08f);

        if (hasLastScreenRay)
        {
            Gizmos.color = gizmoScreenRayColor;

            float d = Mathf.Max(0.1f, lastScreenRayDrawDist);
            Vector3 rayEnd = lastScreenRayOrigin + lastScreenRayDir * d;

            Gizmos.DrawLine(lastScreenRayOrigin, rayEnd);
            Gizmos.DrawWireSphere(rayEnd, 0.05f);
        }
    }
}