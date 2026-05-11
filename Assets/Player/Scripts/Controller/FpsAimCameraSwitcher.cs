using System.Reflection;
using UnityEngine;

[DisallowMultipleComponent]
public class FpsAimCameraSwitcher : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerFpsView playerFpsView;

    [Header("Cameras")]
    [SerializeField] private Behaviour normalFpsCamera;
    [SerializeField] private Behaviour aimingFpsCamera;

    [Header("Priority")]
    [SerializeField] private bool usePrioritySwitch = true;
    [SerializeField] private int inactivePriority = 10;
    [SerializeField] private int activePriority = 30;

    [Header("View Gate")]
    [SerializeField] private SwitchCamView switchCamView;
    [SerializeField] private bool onlyWorkInFirstPersonView = true;

    [Header("Optional")]
    [SerializeField] private bool onlyWorkWhenNormalCameraActive = true;
    [SerializeField] private bool disableAimingCameraOutsideFps = true;

    private PropertyInfo normalPriorityProperty;
    private PropertyInfo aimingPriorityProperty;

    private FieldInfo normalPriorityField;
    private FieldInfo aimingPriorityField;

    private void Reset()
    {
        normalFpsCamera = FindCameraBehaviourOnThisObject();

        if (playerFpsView == null)
            playerFpsView = FindObjectOfType<PlayerFpsView>();

        if (switchCamView == null)
            switchCamView = FindObjectOfType<SwitchCamView>();
    }

    private void Awake()
    {
        if (normalFpsCamera == null)
            normalFpsCamera = FindCameraBehaviourOnThisObject();

        if (playerFpsView == null)
            playerFpsView = FindObjectOfType<PlayerFpsView>();

        if (switchCamView == null)
            switchCamView = FindObjectOfType<SwitchCamView>();

        CachePriorityAccess();
        ApplyNotInFpsState();
    }

    private void OnEnable()
    {
        CachePriorityAccess();

        if (!IsFirstPersonAllowed())
            ApplyNotInFpsState();
    }

    private void OnDisable()
    {
        ApplyNotInFpsState();
    }

    private void LateUpdate()
    {
        if (!IsFirstPersonAllowed())
        {
            ApplyNotInFpsState();
            return;
        }

        if (onlyWorkWhenNormalCameraActive &&
            normalFpsCamera != null &&
            !normalFpsCamera.gameObject.activeInHierarchy)
        {
            ApplyNotInFpsState();
            return;
        }

        bool aimHeld = playerFpsView != null && playerFpsView.IsViewAiming;

        ApplyCameraState(aimHeld);
    }

    private bool IsFirstPersonAllowed()
    {
        if (!onlyWorkInFirstPersonView)
            return true;

        if (switchCamView == null)
            return false;

        return switchCamView.IsFirstPerson;
    }

    private Behaviour FindCameraBehaviourOnThisObject()
    {
        Behaviour[] behaviours = GetComponents<Behaviour>();

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] == null)
                continue;

            if (behaviours[i] == this)
                continue;

            string typeName = behaviours[i].GetType().Name;

            if (typeName.Contains("Cinemachine"))
                return behaviours[i];
        }

        return null;
    }

    private void CachePriorityAccess()
    {
        normalPriorityProperty = null;
        aimingPriorityProperty = null;
        normalPriorityField = null;
        aimingPriorityField = null;

        CachePriorityAccessForCamera(
            normalFpsCamera,
            out normalPriorityProperty,
            out normalPriorityField
        );

        CachePriorityAccessForCamera(
            aimingFpsCamera,
            out aimingPriorityProperty,
            out aimingPriorityField
        );
    }

    private void CachePriorityAccessForCamera(
        Behaviour cameraBehaviour,
        out PropertyInfo priorityProperty,
        out FieldInfo priorityField)
    {
        priorityProperty = null;
        priorityField = null;

        if (cameraBehaviour == null)
            return;

        System.Type type = cameraBehaviour.GetType();

        priorityProperty = type.GetProperty(
            "Priority",
            BindingFlags.Instance | BindingFlags.Public
        );

        if (priorityProperty != null &&
            priorityProperty.PropertyType == typeof(int) &&
            priorityProperty.CanWrite)
        {
            return;
        }

        priorityProperty = null;

        priorityField = type.GetField(
            "Priority",
            BindingFlags.Instance | BindingFlags.Public
        );

        if (priorityField != null &&
            priorityField.FieldType == typeof(int))
        {
            return;
        }

        priorityField = null;
    }

    private void SetCameraPriority(
        Behaviour cameraBehaviour,
        PropertyInfo priorityProperty,
        FieldInfo priorityField,
        int priority)
    {
        if (cameraBehaviour == null)
            return;

        if (priorityProperty != null)
        {
            priorityProperty.SetValue(cameraBehaviour, priority);
            return;
        }

        if (priorityField != null)
            priorityField.SetValue(cameraBehaviour, priority);
    }

    private bool HasPriorityAccess()
    {
        bool hasNormal =
            normalPriorityProperty != null ||
            normalPriorityField != null;

        bool hasAiming =
            aimingPriorityProperty != null ||
            aimingPriorityField != null;

        return hasNormal && hasAiming;
    }

    private void ApplyNotInFpsState()
    {
        if (usePrioritySwitch && HasPriorityAccess())
        {
            SetCameraPriority(
                normalFpsCamera,
                normalPriorityProperty,
                normalPriorityField,
                inactivePriority
            );

            SetCameraPriority(
                aimingFpsCamera,
                aimingPriorityProperty,
                aimingPriorityField,
                inactivePriority
            );
        }

        if (disableAimingCameraOutsideFps && aimingFpsCamera != null)
            aimingFpsCamera.enabled = false;
    }

    private void ApplyCameraState(bool aiming)
    {
        if (usePrioritySwitch && HasPriorityAccess())
        {
            if (normalFpsCamera != null)
                normalFpsCamera.enabled = true;

            if (aimingFpsCamera != null)
                aimingFpsCamera.enabled = true;

            SetCameraPriority(
                normalFpsCamera,
                normalPriorityProperty,
                normalPriorityField,
                aiming ? inactivePriority : activePriority
            );

            SetCameraPriority(
                aimingFpsCamera,
                aimingPriorityProperty,
                aimingPriorityField,
                aiming ? activePriority : inactivePriority
            );

            return;
        }

        if (normalFpsCamera != null)
            normalFpsCamera.enabled = !aiming;

        if (aimingFpsCamera != null)
            aimingFpsCamera.enabled = aiming;
    }
}