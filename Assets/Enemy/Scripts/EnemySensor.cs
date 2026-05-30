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
    [SerializeField] private EnemyStatus enemyStatus;

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

    [Header("Damage Reveal")]
    [SerializeField] private bool allowDamageReveal = true;
    [SerializeField] private bool damageRevealIgnoresInvisibility = true;
    [Min(0f)] [SerializeField] private float defaultDamageRevealDuration = 2f;

    [Header("Squad Reveal")]
    [SerializeField] private bool shareCanSeeTargetWithSquad = true;
    [SerializeField] private bool acceptSquadReveal = true;
    [Min(0f)] [SerializeField] private float squadRevealDuration = 2f;
    [Min(0f)] [SerializeField] private float squadRevealShareCooldown = 0.25f;

    [Header("Last Known Position Update Rules")]
    [SerializeField] private bool rememberLastKnownPosition = true;
    [SerializeField] private bool updateLastKnownInMainVision = true;
    [SerializeField] private bool updateLastKnownInLostSightVision = true;
    [SerializeField] private bool updateLastKnownDuringLostSightDelay = true;

    [SerializeField] private Vector3 lastKnownPosition;
    [SerializeField] private bool hasLastKnownPosition;
    [SerializeField] private bool lastKnownPositionLockedByCamo;

    [Header("Occlusion")]
    [SerializeField] private bool useOcclusion = true;
    [SerializeField] private LayerMask obstructionMask = ~0;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
    [SerializeField] private float occlusionPadding = 0.05f;

    [Header("Smoke Vision Block")]
    [SerializeField] private bool useSmokeVisionBlockers = true;
    [SerializeField] private bool debugSmokeVisionBlock = false;

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
    [SerializeField] private bool damageRevealActive;
    [SerializeField] private float damageRevealEndTime = -999f;
    [SerializeField] private bool canShareCurrentRevealWithSquad;
    private float nextSquadRevealShareTime = -999f;

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
        if (enemyStatus == null)
            enemyStatus = GetComponent<EnemyStatus>();

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
            UploadCanSeeTargetStatus();
            UploadSmokeBlockingVisionStatus(false);
            canShareCurrentRevealWithSquad = false;
            distanceToTarget = 0f;
            targetLocked = false;
            lostSightTimer = 0f;
            targetInMainVision = false;
            targetInLostSightVision = false;
            hasLineOfSight = false;
            return;
        }

        distanceToTarget = Vector3.Distance(transform.position, target.position);

        bool smokeBlockingVision = IsTargetLineBlockedBySmoke();
        UploadSmokeBlockingVisionStatus(smokeBlockingVision);

        if (smokeBlockingVision)
        {
            canSeeTarget = false;
            UploadCanSeeTargetStatus();
            canShareCurrentRevealWithSquad = false;
            targetInMainVision = false;
            targetInLostSightVision = false;
            hasLineOfSight = false;
            return;
        }

        if (IsTargetInvisible())
        {
            if (!lastKnownPositionLockedByCamo)
                LockLastKnownPositionAt(target.position, target.rotation);

            canSeeTarget = false;
            UploadCanSeeTargetStatus();
            canShareCurrentRevealWithSquad = false;
            targetLocked = false;
            lostSightTimer = 0f;
            targetInMainVision = false;
            targetInLostSightVision = false;
            hasLineOfSight = false;
            return;
        }

        if (IsDamageRevealActive())
        {
            canSeeTarget = true;
            UploadCanSeeTargetStatus();
            TryShareTargetWithSquad();
            targetLocked = true;
            lostSightTimer = 0f;
            targetInMainVision = true;
            targetInLostSightVision = false;
            hasLineOfSight = true;
            UnlockCamoLastKnownPosition();
            UpdateLastKnownPositionMarker();
            return;
        }

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
            UploadCanSeeTargetStatus();
            canShareCurrentRevealWithSquad = true;
            TryShareTargetWithSquad();
            targetLocked = true;
            lostSightTimer = 0f;

            UnlockCamoLastKnownPosition();

            if (updateLastKnownInMainVision)
                UpdateLastKnownPositionMarker();

            return;
        }

        if (targetInLostSightVision)
        {
            canSeeTarget = true;
            UploadCanSeeTargetStatus();
            canShareCurrentRevealWithSquad = true;
            TryShareTargetWithSquad();
            targetLocked = true;
            lostSightTimer = 0f;

            UnlockCamoLastKnownPosition();

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
                UploadCanSeeTargetStatus();
                canShareCurrentRevealWithSquad = true;
                TryShareTargetWithSquad();

                if (updateLastKnownDuringLostSightDelay)
                    UpdateLastKnownPositionMarker();

                return;
            }

            if (updateLastKnownDuringLostSightDelay)
                UpdateLastKnownPositionMarker();

            canSeeTarget = false;
            UploadCanSeeTargetStatus();
            targetLocked = false;
            return;
        }

        canSeeTarget = false;
        UploadCanSeeTargetStatus();
        canShareCurrentRevealWithSquad = false;
    }

    public void RevealTargetFromDamage(float duration = -1f)
    {
        if (!allowDamageReveal)
            return;

        Transform revealTarget = target;

        if (revealTarget == null && PlayerStatus.Instance != null)
            revealTarget = PlayerStatus.Instance.transform;

        if (revealTarget == null)
            return;

        target = revealTarget;

        if (!damageRevealIgnoresInvisibility && IsTargetInvisible())
            return;

        float revealDuration = duration >= 0f
            ? duration
            : defaultDamageRevealDuration;

        revealDuration = Mathf.Max(0f, revealDuration);

        ApplyTimedReveal(revealTarget, revealDuration, true);
        TryShareTargetWithSquad();
    }

    public void RevealTargetFromSquad(Transform revealTarget, float duration = -1f)
    {
        if (!acceptSquadReveal)
            return;

        if (revealTarget == null)
            return;

        if (!damageRevealIgnoresInvisibility)
        {
            PlayerStatus playerStatus = revealTarget.GetComponentInParent<PlayerStatus>();

            if (playerStatus == null)
                playerStatus = revealTarget.GetComponentInChildren<PlayerStatus>();

            if (playerStatus != null && playerStatus.IsInvisible)
                return;
        }

        float revealDuration = duration >= 0f
            ? duration
            : squadRevealDuration;

        ApplyTimedReveal(revealTarget, revealDuration, false);
    }

    private void ApplyTimedReveal(Transform revealTarget, float duration, bool shareWithSquad)
    {
        target = revealTarget;

        if (IsTargetInvisible())
        {
            if (!lastKnownPositionLockedByCamo)
                LockLastKnownPositionAt(target.position, target.rotation);

            canSeeTarget = false;
            targetLocked = false;
            lostSightTimer = 0f;
            damageRevealActive = false;
            canShareCurrentRevealWithSquad = false;
            targetInMainVision = false;
            targetInLostSightVision = false;
            hasLineOfSight = false;
            UploadCanSeeTargetStatus();
            return;
        }

        duration = Mathf.Max(0f, duration);
        damageRevealActive = true;
        damageRevealEndTime = Time.time + duration;
        canShareCurrentRevealWithSquad = shareWithSquad;
        canSeeTarget = true;
        targetLocked = true;
        lostSightTimer = 0f;

        UnlockCamoLastKnownPosition();
        UpdateLastKnownPositionMarker();
        UploadCanSeeTargetStatus();
    }

    private bool IsDamageRevealActive()
    {
        if (!damageRevealActive)
            return false;

        if (Time.time <= damageRevealEndTime)
            return true;

        damageRevealActive = false;
        canShareCurrentRevealWithSquad = false;
        return false;
    }

    private void TryShareTargetWithSquad()
    {
        if (!shareCanSeeTargetWithSquad)
            return;

        if (!canShareCurrentRevealWithSquad)
            return;

        if (target == null)
            return;

        if (Time.time < nextSquadRevealShareTime)
            return;

        if (enemyStatus == null)
            enemyStatus = GetComponent<EnemyStatus>();

        SquadMember squadMember = enemyStatus != null
            ? enemyStatus.SquadMember
            : GetComponent<SquadMember>();

        if (squadMember == null || squadMember.SquadManager == null)
            return;

        nextSquadRevealShareTime = Time.time + Mathf.Max(0f, squadRevealShareCooldown);
        squadMember.SquadManager.RevealTargetToTeammates(
            squadMember,
            target,
            squadRevealDuration
        );
    }

    private void UploadCanSeeTargetStatus()
    {
        if (enemyStatus == null)
            enemyStatus = GetComponent<EnemyStatus>();

        if (enemyStatus != null)
            enemyStatus.SetCanSeeTarget(canSeeTarget);
    }

    private void UploadSmokeBlockingVisionStatus(bool value)
    {
        if (enemyStatus == null)
            enemyStatus = GetComponent<EnemyStatus>();

        if (enemyStatus != null)
            enemyStatus.SetSmokeBlockingVision(value);
    }

    private bool IsTargetInvisible()
    {
        if (target == null)
            return false;

        PlayerStatus playerStatus = target.GetComponentInParent<PlayerStatus>();

        if (playerStatus == null)
            playerStatus = target.GetComponentInChildren<PlayerStatus>();

        return playerStatus != null && playerStatus.IsInvisible;
    }

    private void UpdateLastKnownPositionMarker()
    {
        if (!rememberLastKnownPosition || target == null)
            return;

        if (lastKnownPositionLockedByCamo)
            return;

        SetLastKnownPosition(target.position, target.rotation);
    }

    private void SetLastKnownPosition(
        Vector3 position,
        Quaternion rotation,
        bool forceMarkerUpdate = false)
    {
        lastKnownPosition = position;
        hasLastKnownPosition = true;

        if (lastKnownPositionMarker != null &&
            (updateMarkerWhileTargetLocked || forceMarkerUpdate))
        {
            lastKnownPositionMarker.position = position;
            lastKnownPositionMarker.rotation = rotation;
        }
    }

    private void LockLastKnownPositionAt(Vector3 position, Quaternion rotation)
    {
        SetLastKnownPosition(position, rotation, true);
        lastKnownPositionLockedByCamo = true;
    }

    private void UnlockCamoLastKnownPosition()
    {
        lastKnownPositionLockedByCamo = false;
    }

    public void LockLastKnownPositionForInvisibleTarget(Transform invisibleTarget, Vector3 position)
    {
        if (invisibleTarget == null)
            return;

        if (target != null && !IsSameTargetHierarchy(target, invisibleTarget))
            return;

        if (target == null)
            target = invisibleTarget;

        LockLastKnownPositionAt(position, invisibleTarget.rotation);
        canSeeTarget = false;
        targetLocked = false;
        lostSightTimer = 0f;
        damageRevealActive = false;
        canShareCurrentRevealWithSquad = false;
        targetInMainVision = false;
        targetInLostSightVision = false;
        hasLineOfSight = false;

        UploadCanSeeTargetStatus();
    }

    private bool IsSameTargetHierarchy(Transform first, Transform second)
    {
        if (first == null || second == null)
            return false;

        if (first == second)
            return true;

        return first.IsChildOf(second) ||
               second.IsChildOf(first) ||
               first.root == second.root;
    }

    public void ClearLastKnownPosition()
    {
        hasLastKnownPosition = false;
        lastKnownPositionLockedByCamo = false;
    }

    public void ClearTargetLock()
    {
        canSeeTarget = false;
        targetLocked = false;
        lostSightTimer = 0f;
        damageRevealActive = false;
        canShareCurrentRevealWithSquad = false;
        lastKnownPositionLockedByCamo = false;
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

        if (IsSmokeVisionBlocking(eyePosition, targetPosition))
        {
            if (debugDrawRay && Application.isPlaying)
                Debug.DrawLine(eyePosition, targetPosition, Color.gray, debugRayDuration, true);

            return false;
        }

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

    private bool IsTargetLineBlockedBySmoke()
    {
        if (target == null)
            return false;

        return IsSmokeVisionBlocking(
            GetEyePosition(),
            GetTargetVisionPosition()
        );
    }

    private bool IsSmokeVisionBlocking(Vector3 eyePosition, Vector3 targetPosition)
    {
        if (!useSmokeVisionBlockers)
            return false;

        SmokeVisionBlocker blocker = SmokeVisionBlocker.GetBlockingBlocker(
            eyePosition,
            targetPosition
        );

        if (blocker == null)
            return false;

        if (debugSmokeVisionBlock)
        {
            Debug.Log(
                $"[EnemySensor] Smoke blocked vision. Enemy={name}, Smoke={blocker.name}, ActiveSmokeCount={SmokeVisionBlocker.ActiveCount}",
                this
            );
        }

        return true;
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
