using System.Reflection;
using UnityEngine;

[DisallowMultipleComponent]
public class TpsAimCameraSwitcher : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerTpsView playerTpsView;

    [Header("Cameras")]
    [SerializeField] private Behaviour normalTpsCamera;
    [SerializeField] private Behaviour aimingTpsCamera;

    [Header("Priority")]
    [SerializeField] private bool usePrioritySwitch = true;
    [SerializeField] private int inactivePriority = 10;
    [SerializeField] private int activePriority = 30;

    [Header("View Gate")]
    [SerializeField] private SwitchCamView switchCamView;
    [SerializeField] private bool onlyWorkInThirdPersonView = true;

    [Header("Optional")]
    [SerializeField] private bool onlyWorkWhenNormalCameraActive = true;
    [SerializeField] private bool disableAimingCameraOutsideTps = true;

    private PropertyInfo normalPriorityProperty;
    private PropertyInfo aimingPriorityProperty;

    private FieldInfo normalPriorityField;
    private FieldInfo aimingPriorityField;

    private void Reset()
    {
        normalTpsCamera = FindCameraBehaviourOnThisObject();

        if (playerTpsView == null)
            playerTpsView = FindObjectOfType<PlayerTpsView>();

        if (switchCamView == null)
            switchCamView = FindObjectOfType<SwitchCamView>();
    }

    private void Awake()
    {
        if (normalTpsCamera == null)
            normalTpsCamera = FindCameraBehaviourOnThisObject();

        if (playerTpsView == null)
            playerTpsView = FindObjectOfType<PlayerTpsView>();

        if (switchCamView == null)
            switchCamView = FindObjectOfType<SwitchCamView>();

        CachePriorityAccess();
        ApplyNotInTpsState();
    }

    private void OnEnable()
    {
        CachePriorityAccess();

        if (!IsThirdPersonAllowed())
            ApplyNotInTpsState();
    }

    private void OnDisable()
    {
        ApplyNotInTpsState();
    }

    private void LateUpdate()
    {
        if (!IsThirdPersonAllowed())
        {
            ApplyNotInTpsState();
            return;
        }

        if (onlyWorkWhenNormalCameraActive &&
            normalTpsCamera != null &&
            !normalTpsCamera.gameObject.activeInHierarchy)
        {
            ApplyNotInTpsState();
            return;
        }

        bool realAimHeld = playerTpsView != null && playerTpsView.IsRealAimHeld;

        ApplyCameraState(realAimHeld);
    }

    private bool IsThirdPersonAllowed()
    {
        if (!onlyWorkInThirdPersonView)
            return true;

        if (switchCamView == null)
            return false;

        return switchCamView.IsThirdPerson;
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
            normalTpsCamera,
            out normalPriorityProperty,
            out normalPriorityField
        );

        CachePriorityAccessForCamera(
            aimingTpsCamera,
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

    private void ApplyNotInTpsState()
    {
        if (usePrioritySwitch && HasPriorityAccess())
        {
            SetCameraPriority(
                normalTpsCamera,
                normalPriorityProperty,
                normalPriorityField,
                inactivePriority
            );

            SetCameraPriority(
                aimingTpsCamera,
                aimingPriorityProperty,
                aimingPriorityField,
                inactivePriority
            );
        }

        if (disableAimingCameraOutsideTps && aimingTpsCamera != null)
            aimingTpsCamera.enabled = false;
    }

    private void ApplyCameraState(bool aiming)
    {
        if (usePrioritySwitch && HasPriorityAccess())
        {
            if (normalTpsCamera != null)
                normalTpsCamera.enabled = true;

            if (aimingTpsCamera != null)
                aimingTpsCamera.enabled = true;

            SetCameraPriority(
                normalTpsCamera,
                normalPriorityProperty,
                normalPriorityField,
                aiming ? inactivePriority : activePriority
            );

            SetCameraPriority(
                aimingTpsCamera,
                aimingPriorityProperty,
                aimingPriorityField,
                aiming ? activePriority : inactivePriority
            );

            return;
        }

        if (normalTpsCamera != null)
            normalTpsCamera.enabled = !aiming;

        if (aimingTpsCamera != null)
            aimingTpsCamera.enabled = aiming;
    }
}