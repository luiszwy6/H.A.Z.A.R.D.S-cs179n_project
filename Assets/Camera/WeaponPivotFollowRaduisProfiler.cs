using UnityEngine;

[DisallowMultipleComponent]
public class WeaponPivotFollowRadiusProfile : MonoBehaviour
{
    [Header("Apply")]
    [SerializeField] private bool applyOnEquip = true;
    [SerializeField] private bool snapAfterApply = false;

    [Header("Follow")]
    [SerializeField] private bool overrideFollowSpeed = false;
    [SerializeField] private float followSpeed = 20f;

    [Header("Radius Limit")]
    [SerializeField] private bool overrideMaxRadius = true;
    [SerializeField] private float maxRadius = 3f;

    [SerializeField] private bool overrideLimitOnXZOnly = false;
    [SerializeField] private bool limitOnXZOnly = true;

    [Header("Vertical Lock")]
    [SerializeField] private bool overrideLockVerticalPosition = false;
    [SerializeField] private bool lockVerticalPosition = true;

    [SerializeField] private bool overrideVerticalMode = false;
    [SerializeField] private PivotFollowWithRadiusLimit.VerticalLockMode verticalMode =
        PivotFollowWithRadiusLimit.VerticalLockMode.KeepInitialY;

    [SerializeField] private bool overrideFixedWorldY = false;
    [SerializeField] private float fixedWorldY = 0f;

    [SerializeField] private bool overrideCenterYOffset = false;
    [SerializeField] private float centerYOffset = 0f;

    [Header("When Follow Target Is Inactive")]
    [SerializeField] private bool overrideReturnToOrbitCenterWhenInactive = false;
    [SerializeField] private bool returnToOrbitCenterWhenInactive = true;

    [SerializeField] private bool overrideKeepLastValidPositionWhenInactive = false;
    [SerializeField] private bool keepLastValidPositionWhenInactive = false;

    public bool ApplyOnEquip => applyOnEquip;
    public bool SnapAfterApply => snapAfterApply;

    public PivotFollowWithRadiusLimit.RuntimeSettings BuildSettings(
        PivotFollowWithRadiusLimit.RuntimeSettings baseSettings)
    {
        PivotFollowWithRadiusLimit.RuntimeSettings result = baseSettings;

        if (overrideFollowSpeed)
            result.followSpeed = Mathf.Max(0f, followSpeed);

        if (overrideMaxRadius)
            result.maxRadius = Mathf.Max(0f, maxRadius);

        if (overrideLimitOnXZOnly)
            result.limitOnXZOnly = limitOnXZOnly;

        if (overrideLockVerticalPosition)
            result.lockVerticalPosition = lockVerticalPosition;

        if (overrideVerticalMode)
            result.verticalMode = verticalMode;

        if (overrideFixedWorldY)
            result.fixedWorldY = fixedWorldY;

        if (overrideCenterYOffset)
            result.centerYOffset = centerYOffset;

        if (overrideReturnToOrbitCenterWhenInactive)
            result.returnToOrbitCenterWhenInactive = returnToOrbitCenterWhenInactive;

        if (overrideKeepLastValidPositionWhenInactive)
            result.keepLastValidPositionWhenInactive = keepLastValidPositionWhenInactive;

        return result;
    }
}