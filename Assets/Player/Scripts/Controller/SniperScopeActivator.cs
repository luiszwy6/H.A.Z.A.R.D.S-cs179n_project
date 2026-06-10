using System.Reflection;
using UnityEngine;

[DisallowMultipleComponent]
public class SniperScopeActivator : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private SwitchCamView switchCamView;
    [SerializeField] private PlayerTpsView tpsView;
    [SerializeField] private SRShootSettings sniperShootSettings;

    [Header("Camera")]
    [SerializeField] private Behaviour sniperAim1Camera;

    [Header("Priority")]
    [SerializeField] private bool usePrioritySwitch = true;
    [SerializeField] private int inactivePriority = 10;
    [SerializeField] private int activePriority = 30;

    private PropertyInfo priorityProperty;
    private FieldInfo priorityField;
    private bool wasActive;

    private void Awake()
    {
        if (switchCamView == null)
            switchCamView = GetComponent<SwitchCamView>();

        if (tpsView == null)
            tpsView = GetComponent<PlayerTpsView>();

        CachePriorityAccess();
        SetPriority(inactivePriority);
    }

    private void OnEnable()
    {
        wasActive = false;
        SetPriority(inactivePriority);
    }

    private void OnDisable()
    {
        SetPriority(inactivePriority);
        if (wasActive)
        {
            wasActive = false;
            switchCamView?.SetSniperAimUI(false);
        }
    }

    private void LateUpdate()
    {
        bool shouldBeActive =
            switchCamView != null && switchCamView.IsThirdPerson &&
            tpsView != null && tpsView.IsViewAiming &&
            IsSniperEquipped();

        if (shouldBeActive == wasActive) return;

        wasActive = shouldBeActive;
        SetPriority(shouldBeActive ? activePriority : inactivePriority);
        switchCamView.SetSniperAimUI(shouldBeActive);
    }

    private bool IsSniperEquipped() =>
        sniperShootSettings != null &&
        sniperShootSettings.enabled &&
        sniperShootSettings.gameObject.activeInHierarchy;

    private void CachePriorityAccess()
    {
        priorityProperty = null;
        priorityField    = null;

        if (sniperAim1Camera == null) return;

        System.Type type = sniperAim1Camera.GetType();

        priorityProperty = type.GetProperty("Priority", BindingFlags.Instance | BindingFlags.Public);
        if (priorityProperty != null && priorityProperty.PropertyType == typeof(int) && priorityProperty.CanWrite)
            return;
        priorityProperty = null;

        priorityField = type.GetField("Priority", BindingFlags.Instance | BindingFlags.Public);
        if (priorityField != null && priorityField.FieldType == typeof(int))
            return;
        priorityField = null;
    }

    private void SetPriority(int priority)
    {
        if (sniperAim1Camera == null) return;

        if (!usePrioritySwitch || (priorityProperty == null && priorityField == null))
        {
            sniperAim1Camera.enabled = priority == activePriority;
            return;
        }

        sniperAim1Camera.enabled = true;

        if (priorityProperty != null)
            priorityProperty.SetValue(sniperAim1Camera, priority);
        else
            priorityField.SetValue(sniperAim1Camera, priority);
    }
}
