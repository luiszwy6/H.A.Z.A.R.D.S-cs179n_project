using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class EnemySensor : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private Transform targetVisionPoint;

    [Header("Last Known Position Marker")]
    [SerializeField] private Transform lastKnownPositionMarker;
    [SerializeField] private bool autoCreateLastKnownPositionMarker = true;
    [SerializeField] private string lastKnownMarkerName = "PlayerLastKnownPosition";
    [SerializeField] private bool updateMarkerWhileTargetLocked = true;

    [Header("Eye")]
    [SerializeField] private Transform eyeTransform;
    [SerializeField] private Vector3 eyeOffset = new Vector3(0f, 1.6f, 0f);

    [Header("Vision")]
    [SerializeField] private float viewRadius = 18f;

    [Range(0f, 360f)]
    [SerializeField] private float viewAngle = 100f;

    [Header("Lost Sight Vision")]
    [SerializeField] private bool useLostSightVision = true;
    [SerializeField] private float lostSightVisionRadius = 28f;

    [Range(0f, 360f)]
    [SerializeField] private float lostSightVisionAngle = 160f;

    [SerializeField] private float lostSightDelay = 2f;
    [SerializeField] private bool requireLineOfSightForLostSightVision = true;

    [Header("Last Known Position Update Rules")]
    [SerializeField] private bool rememberLastKnownPosition = true;
    [SerializeField] private bool updateLastKnownInMainVision = true;
    [SerializeField] private bool updateLastKnownInLostSightVision = true;
    [SerializeField] private bool updateLastKnownDuringLostSightDelay = true;

    [SerializeField] private Vector3 lastKnownPosition;
    [SerializeField] private bool hasLastKnownPosition;

    [Header("Occlusion")]
    [SerializeField] private bool useOcclusion = true;
    [SerializeField] private LayerMask obstructionMask = ~0;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
    [SerializeField] private float occlusionPadding = 0.05f;

    [Header("Auto Update")]
    [SerializeField] private bool autoUpdate = true;

    [Header("Debug")]
    [SerializeField] private bool debugDrawRay = true;
    [SerializeField] private bool debugDrawGizmos = true;
    [SerializeField] private float debugRayDuration = 0f;

    [SerializeField] private bool canSeeTarget;
    [SerializeField] private float distanceToTarget;

    [Header("Debug Lost Sight")]
    [SerializeField] private bool targetLocked;
    [SerializeField] private float lostSightTimer;
    [SerializeField] private bool targetInMainVision;
    [SerializeField] private bool targetInLostSightVision;
    [SerializeField] private bool hasLineOfSight;

    public Transform Target
    {
        get { return target; }
    }

    public Transform LastKnownPositionMarker
    {
        get { return lastKnownPositionMarker; }
    }

    public bool CanSeeTarget
    {
        get { return canSeeTarget; }
    }

    public float DistanceToTarget
    {
        get { return distanceToTarget; }
    }

    public Vector3 LastKnownPosition
    {
        get
        {
            if (lastKnownPositionMarker != null)
                return lastKnownPositionMarker.position;

            return lastKnownPosition;
        }
    }

    public bool HasLastKnownPosition
    {
        get { return hasLastKnownPosition; }
    }

    public bool TargetLocked
    {
        get { return targetLocked; }
    }

    public float LostSightTimer
    {
        get { return lostSightTimer; }
    }

    public bool TargetInMainVision
    {
        get { return targetInMainVision; }
    }

    public bool TargetInLostSightVision
    {
        get { return targetInLostSightVision; }
    }

    private void Awake()
    {
        EnsureLastKnownPositionMarker();
    }

    private void Update()
    {
        if (autoUpdate)
            RefreshSensor();
    }

    private void EnsureLastKnownPositionMarker()
    {
        if (lastKnownPositionMarker != null)
            return;

        if (!autoCreateLastKnownPositionMarker)
            return;

        GameObject marker = new GameObject(lastKnownMarkerName);
        marker.transform.SetParent(transform);
        marker.transform.position = transform.position;
        marker.transform.rotation = Quaternion.identity;

        lastKnownPositionMarker = marker.transform;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    public void SetTargetVisionPoint(Transform newTargetVisionPoint)
    {
        targetVisionPoint = newTargetVisionPoint;
    }

    public void RefreshSensor()
    {
        EnsureLastKnownPositionMarker();

        if (target == null)
        {
            canSeeTarget = false;
            distanceToTarget = 0f;
            targetLocked = false;
            lostSightTimer = 0f;
            targetInMainVision = false;
            targetInLostSightVision = false;
            hasLineOfSight = false;
            return;
        }

        distanceToTarget = Vector3.Distance(transform.position, target.position);
        hasLineOfSight = HasLineOfSight();

        targetInMainVision =
            IsTargetInsideViewSector(viewRadius, viewAngle) &&
            hasLineOfSight;

        targetInLostSightVision =
            useLostSightVision &&
            targetLocked &&
            IsTargetInsideViewSector(lostSightVisionRadius, lostSightVisionAngle) &&
            (!requireLineOfSightForLostSightVision || hasLineOfSight);

        if (targetInMainVision)
        {
            canSeeTarget = true;
            targetLocked = true;
            lostSightTimer = 0f;

            if (updateLastKnownInMainVision)
                UpdateLastKnownPositionMarker();

            return;
        }

        if (targetInLostSightVision)
        {
            canSeeTarget = true;
            targetLocked = true;
            lostSightTimer = 0f;

            if (updateLastKnownInLostSightVision)
                UpdateLastKnownPositionMarker();

            return;
        }

        if (targetLocked)
        {
            lostSightTimer += Time.deltaTime;

            if (lostSightTimer < lostSightDelay)
            {
                canSeeTarget = true;

                if (updateLastKnownDuringLostSightDelay)
                    UpdateLastKnownPositionMarker();

                return;
            }

            if (updateLastKnownDuringLostSightDelay)
                UpdateLastKnownPositionMarker();

            canSeeTarget = false;
            targetLocked = false;
            return;
        }

        canSeeTarget = false;
    }

    private void UpdateLastKnownPositionMarker()
    {
        if (!rememberLastKnownPosition || target == null)
            return;

        lastKnownPosition = target.position;
        hasLastKnownPosition = true;

        if (lastKnownPositionMarker != null && updateMarkerWhileTargetLocked)
        {
            lastKnownPositionMarker.position = target.position;
            lastKnownPositionMarker.rotation = target.rotation;
        }
    }

    public void ClearLastKnownPosition()
    {
        hasLastKnownPosition = false;
    }

    public void ClearTargetLock()
    {
        canSeeTarget = false;
        targetLocked = false;
        lostSightTimer = 0f;
    }

    public Vector3 GetEyePosition()
    {
        if (eyeTransform != null)
            return eyeTransform.position;

        return transform.position + eyeOffset;
    }

    public Vector3 GetTargetVisionPosition()
    {
        if (targetVisionPoint != null)
            return targetVisionPoint.position;

        if (target != null)
            return target.position;

        return Vector3.zero;
    }

    public bool CheckCanSeeTarget()
    {
        RefreshSensor();
        return canSeeTarget;
    }

    private bool IsTargetInsideViewSector(float radius, float angle)
    {
        Vector3 eyePosition = GetEyePosition();
        Vector3 targetPosition = GetTargetVisionPosition();

        Vector3 toTarget = targetPosition - eyePosition;
        toTarget.y = 0f;

        float flatDistance = toTarget.magnitude;

        if (flatDistance > radius)
            return false;

        if (flatDistance <= 0.0001f)
            return true;

        Vector3 forward = GetFlatForward();

        float currentAngle = Vector3.Angle(forward, toTarget.normalized);

        if (currentAngle > angle * 0.5f)
            return false;

        return true;
    }

    private bool HasLineOfSight()
    {
        if (!useOcclusion)
            return true;

        if (target == null)
            return false;

        Vector3 eyePosition = GetEyePosition();
        Vector3 targetPosition = GetTargetVisionPosition();

        Vector3 toTarget = targetPosition - eyePosition;
        float distance = toTarget.magnitude;

        if (distance <= 0.0001f)
            return true;

        float rayDistance = Mathf.Max(0f, distance - occlusionPadding);

        if (rayDistance <= 0f)
            return true;

        Vector3 direction = toTarget / distance;

        bool blocked = Physics.Raycast(
            eyePosition,
            direction,
            out RaycastHit hit,
            rayDistance,
            obstructionMask,
            triggerInteraction
        );

        if (blocked && IsTargetHit(hit.transform))
            blocked = false;

        if (debugDrawRay && Application.isPlaying)
        {
            if (blocked)
                Debug.DrawLine(eyePosition, hit.point, Color.red, debugRayDuration, true);
            else
                Debug.DrawLine(eyePosition, targetPosition, Color.green, debugRayDuration, true);
        }

        return !blocked;
    }

    private bool IsTargetHit(Transform hitTransform)
    {
        if (hitTransform == null)
            return false;

        if (target != null && (hitTransform == target || hitTransform.IsChildOf(target)))
            return true;

        if (targetVisionPoint != null &&
            (hitTransform == targetVisionPoint || hitTransform.IsChildOf(targetVisionPoint)))
            return true;

        return false;
    }

    private Vector3 GetFlatForward()
    {
        Vector3 forward = eyeTransform != null ? eyeTransform.forward : transform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude <= 0.0001f)
            forward = transform.forward;

        forward.y = 0f;

        if (forward.sqrMagnitude <= 0.0001f)
            forward = Vector3.forward;

        return forward.normalized;
    }

    private void OnDrawGizmosSelected()
    {
#if UNITY_EDITOR
        if (!debugDrawGizmos)
            return;

        Vector3 eyePosition = GetEyePosition();
        Vector3 forward = GetFlatForward();

        DrawViewSector(
            eyePosition,
            forward,
            viewRadius,
            viewAngle,
            canSeeTarget ? Color.green : Color.yellow
        );

        if (useLostSightVision)
        {
            DrawViewSector(
                eyePosition,
                forward,
                lostSightVisionRadius,
                lostSightVisionAngle,
                new Color(1f, 0.4f, 0.1f, 1f)
            );
        }

        if (target != null)
        {
            Gizmos.color = canSeeTarget ? Color.green : Color.yellow;
            Gizmos.DrawLine(eyePosition, GetTargetVisionPosition());
        }

        if (hasLastKnownPosition)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(LastKnownPosition, 0.35f);
        }
#endif
    }

#if UNITY_EDITOR
    private void DrawViewSector(Vector3 origin, Vector3 forward, float radius, float angle, Color color)
    {
        Handles.color = color;

        Quaternion leftRotation = Quaternion.AngleAxis(-angle * 0.5f, Vector3.up);
        Quaternion rightRotation = Quaternion.AngleAxis(angle * 0.5f, Vector3.up);

        Vector3 leftDirection = leftRotation * forward;
        Vector3 rightDirection = rightRotation * forward;

        Handles.DrawWireArc(origin, Vector3.up, leftDirection, angle, radius);
        Handles.DrawLine(origin, origin + leftDirection * radius);
        Handles.DrawLine(origin, origin + rightDirection * radius);
    }
#endif
}