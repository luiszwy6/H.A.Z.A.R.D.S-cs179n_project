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

    [System.Serializable]
    public struct RuntimeSettings
    {
        public float followSpeed;
        public float maxRadius;
        public bool limitOnXZOnly;

        public bool lockVerticalPosition;
        public VerticalLockMode verticalMode;
        public float fixedWorldY;
        public float centerYOffset;

        public bool returnToOrbitCenterWhenInactive;
        public bool keepLastValidPositionWhenInactive;
    }

    [Header("References")]
    [SerializeField] private Transform followTarget;
    [SerializeField] private Transform orbitCenter;

    [Header("Weapon Slots Override")]
    [SerializeField] private PlayerWeaponSlots playerWeaponSlots;
    [SerializeField] private bool useCurrentWeaponProfile = true;
    [SerializeField] private bool restoreDefaultWhenNoWeaponProfile = true;
    [SerializeField] private bool snapWhenWeaponProfileChanges = false;
    [SerializeField] private bool logWeaponProfileChanges = false;

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

    private RuntimeSettings defaultSettings;
    private bool hasDefaultSettings;

    private GameObject lastWeaponObject;
    private WeaponPivotFollowRadiusProfile lastAppliedProfile;

    private void Reset()
    {
        if (orbitCenter == null && transform.parent != null)
            orbitCenter = transform.parent;

        if (playerWeaponSlots == null && transform.root != null)
            playerWeaponSlots = transform.root.GetComponent<PlayerWeaponSlots>();
    }

    private void Awake()
    {
        initialY = transform.position.y;
        lastValidPosition = transform.position;
        hasLastValidPosition = true;

        CaptureDefaultSettings();
    }

    private void OnEnable()
    {
        RefreshWeaponProfile(force: true);
    }

    private void LateUpdate()
    {
        RefreshWeaponProfile(force: false);

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
            1f - Mathf.Exp(-Mathf.Max(0f, followSpeed) * Time.deltaTime)
        );
    }

    private void CaptureDefaultSettings()
    {
        defaultSettings = GetRuntimeSettings();
        hasDefaultSettings = true;
    }

    private void RefreshWeaponProfile(bool force)
    {
        if (!useCurrentWeaponProfile)
            return;

        if (playerWeaponSlots == null)
            return;

        GameObject currentWeapon = playerWeaponSlots.CurrentWeaponObject;

        if (!force && currentWeapon == lastWeaponObject)
            return;

        lastWeaponObject = currentWeapon;

        WeaponPivotFollowRadiusProfile profile = null;

        if (currentWeapon != null)
            profile = currentWeapon.GetComponentInChildren<WeaponPivotFollowRadiusProfile>(true);

        if (profile != null && profile.ApplyOnEquip)
        {
            ApplyWeaponProfile(profile);
            return;
        }

        lastAppliedProfile = null;

        if (restoreDefaultWhenNoWeaponProfile && hasDefaultSettings)
        {
            ApplyRuntimeSettings(defaultSettings, snapWhenWeaponProfileChanges);

            if (logWeaponProfileChanges)
                Debug.Log("[PivotFollowWithRadiusLimit] Restored default pivot follow settings.", this);
        }
    }

    private void ApplyWeaponProfile(WeaponPivotFollowRadiusProfile profile)
    {
        if (profile == null)
            return;

        if (!hasDefaultSettings)
            CaptureDefaultSettings();

        RuntimeSettings settings = profile.BuildSettings(defaultSettings);

        ApplyRuntimeSettings(
            settings,
            snapWhenWeaponProfileChanges || profile.SnapAfterApply
        );

        lastAppliedProfile = profile;

        if (logWeaponProfileChanges)
        {
            Debug.Log(
                $"[PivotFollowWithRadiusLimit] Applied weapon pivot profile from {profile.name}. MaxRadius={maxRadius}, FollowSpeed={followSpeed}",
                this
            );
        }
    }

    public RuntimeSettings GetRuntimeSettings()
    {
        RuntimeSettings settings = new RuntimeSettings
        {
            followSpeed = followSpeed,
            maxRadius = maxRadius,
            limitOnXZOnly = limitOnXZOnly,

            lockVerticalPosition = lockVerticalPosition,
            verticalMode = verticalMode,
            fixedWorldY = fixedWorldY,
            centerYOffset = centerYOffset,

            returnToOrbitCenterWhenInactive = returnToOrbitCenterWhenInactive,
            keepLastValidPositionWhenInactive = keepLastValidPositionWhenInactive
        };

        return settings;
    }

    public void ApplyRuntimeSettings(RuntimeSettings settings, bool snapAfterApply = false)
    {
        followSpeed = Mathf.Max(0f, settings.followSpeed);
        maxRadius = Mathf.Max(0f, settings.maxRadius);
        limitOnXZOnly = settings.limitOnXZOnly;

        lockVerticalPosition = settings.lockVerticalPosition;
        verticalMode = settings.verticalMode;
        fixedWorldY = settings.fixedWorldY;
        centerYOffset = settings.centerYOffset;

        returnToOrbitCenterWhenInactive = settings.returnToOrbitCenterWhenInactive;
        keepLastValidPositionWhenInactive = settings.keepLastValidPositionWhenInactive;

        if (snapAfterApply)
            SnapNow();
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

    public void SetPlayerWeaponSlots(PlayerWeaponSlots newWeaponSlots)
    {
        playerWeaponSlots = newWeaponSlots;
        RefreshWeaponProfile(force: true);
    }

    public void ForceRefreshWeaponProfile()
    {
        RefreshWeaponProfile(force: true);
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