using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class SniperScopeSwitcher : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private SwitchCamView switchCamView;
    [SerializeField] private PlayerTpsView tpsView;
    [SerializeField] private SRShootSettings sniperShootSettings;

    [Header("Camera")]
    [SerializeField] private Behaviour sniperAim2Camera;

    [Header("Priority")]
    [SerializeField] private int inactivePriority = 10;
    [SerializeField] private int activePriority = 40;

    [Header("Input")]
    [SerializeField] private string scopeActionName = "Scope";
    [SerializeField] private Key fallbackKey = Key.None;

    private InputAction scopeAction;
    private PropertyInfo priorityProperty;
    private FieldInfo priorityField;
    private bool scopedIn;

    private void Awake()
    {
        if (playerInput == null)
            playerInput = FindFirstObjectByType<PlayerInput>();

        if (switchCamView == null)
            switchCamView = FindFirstObjectByType<SwitchCamView>();

        if (tpsView == null)
            tpsView = FindFirstObjectByType<PlayerTpsView>();

        CachePriorityAccess();
        SetPriority(inactivePriority);
    }

    private void OnEnable()
    {
        if (playerInput != null)
            scopeAction = playerInput.actions.FindAction(scopeActionName);

        SetPriority(inactivePriority);
        scopedIn = false;
    }

    private void OnDisable()
    {
        scopeAction = null;
        SetPriority(inactivePriority);
        scopedIn = false;
    }

    private void LateUpdate()
    {
        if (!IsConditionMet())
        {
            if (scopedIn)
            {
                scopedIn = false;
                SetPriority(inactivePriority);
            }
            return;
        }

        bool scopePressed = (scopeAction != null && scopeAction.WasPressedThisFrame()) ||
                            (fallbackKey != Key.None && Keyboard.current != null && Keyboard.current[fallbackKey].wasPressedThisFrame);

        if (scopePressed)
        {
            scopedIn = !scopedIn;
            SetPriority(scopedIn ? activePriority : inactivePriority);
        }
    }

    private bool IsConditionMet() =>
        switchCamView != null && switchCamView.IsThirdPerson &&
        tpsView != null && tpsView.IsViewAiming &&
        IsSniperEquipped();

    private bool IsSniperEquipped() =>
        sniperShootSettings != null &&
        sniperShootSettings.enabled &&
        sniperShootSettings.gameObject.activeInHierarchy;

    private void CachePriorityAccess()
    {
        priorityProperty = null;
        priorityField    = null;

        if (sniperAim2Camera == null) return;

        System.Type type = sniperAim2Camera.GetType();

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
        if (sniperAim2Camera == null) return;

        sniperAim2Camera.enabled = true;

        if (priorityProperty != null)
            priorityProperty.SetValue(sniperAim2Camera, priority);
        else if (priorityField != null)
            priorityField.SetValue(sniperAim2Camera, priority);
    }
}
