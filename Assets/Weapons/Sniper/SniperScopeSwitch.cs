using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class SniperScopeSwitch : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private SwitchCamView switchCamView;
    [SerializeField] private PlayerAimSettings playerAimSettings;

    [Header("UI")]
    [SerializeField] private GameObject topDownCrosshairUI;
    [SerializeField] private GameObject topDownScopeUI;

    [Header("Input")]
    [SerializeField] private string scopeActionName = "Scope";

    [Header("Rules")]
    [SerializeField] private bool onlyAllowInTopDown = true;
    [SerializeField] private bool requireAiming = true;
    [SerializeField] private bool closeWhenLeavingTopDown = true;
    [SerializeField] private bool closeWhenStopAiming = true;
    [SerializeField] private bool closeOnDisable = true;

    [Header("Crosshair Display")]
    [SerializeField] private bool showCrosshairOnlyInTopDown = true;
    [SerializeField] private bool showCrosshairOnlyWhenAiming = true;
    [SerializeField] private bool hideCrosshairWhenScopeActive = true;

    [Header("Debug")]
    [SerializeField] private bool logState = false;
    [SerializeField] private bool logMissingAction = false;

    private InputAction scopeAction;
    private bool scopeActive;

    public bool ScopeActive => scopeActive;

    private void Reset()
    {
        Transform root = transform.root;

        if (playerInput == null)
            playerInput = root.GetComponent<PlayerInput>();

        if (switchCamView == null)
            switchCamView = root.GetComponent<SwitchCamView>();

        if (playerAimSettings == null)
            playerAimSettings = root.GetComponent<PlayerAimSettings>();
    }

    private void Awake()
    {
        ResolveReferences();
        CacheInputAction();

        if (!CanKeepScopeOpenNow())
            scopeActive = false;

        ApplyScopeVisuals();
    }

    private void OnEnable()
    {
        ResolveReferences();
        CacheInputAction();

        if (scopeAction != null)
            scopeAction.Enable();
        else if (logMissingAction)
            Debug.LogWarning($"[SniperScopeSwitch] Scope action not found: {scopeActionName}", this);

        if (!CanKeepScopeOpenNow())
            scopeActive = false;

        ApplyScopeVisuals();
    }

    private void OnDisable()
    {
        if (scopeAction != null)
            scopeAction.Disable();

        if (closeOnDisable)
            SetScopeActive(false);
        else
            ApplyScopeVisuals();
    }

    private void Update()
    {
        if (scopeActive && ShouldForceCloseScopeNow())
        {
            SetScopeActive(false);
            return;
        }

        if (scopeAction == null)
            return;

        if (!scopeAction.WasPressedThisFrame())
            return;

        if (!CanOpenScopeNow())
            return;

        ToggleScope();
    }

    private void LateUpdate()
    {
        if (scopeActive && ShouldForceCloseScopeNow())
            SetScopeActive(false);

        ApplyScopeVisuals();
    }

    private void ResolveReferences()
    {
        Transform root = transform.root;

        if (playerInput == null)
            playerInput = root.GetComponent<PlayerInput>();

        if (switchCamView == null)
            switchCamView = root.GetComponent<SwitchCamView>();

        if (playerAimSettings == null)
            playerAimSettings = root.GetComponent<PlayerAimSettings>();
    }

    private void CacheInputAction()
    {
        scopeAction = null;

        if (playerInput == null || playerInput.actions == null)
            return;

        if (string.IsNullOrWhiteSpace(scopeActionName))
            return;

        scopeAction = playerInput.actions.FindAction(scopeActionName, false);
    }

    public void ToggleScope()
    {
        SetScopeActive(!scopeActive);
    }

    public void OpenScope()
    {
        if (!CanOpenScopeNow())
            return;

        SetScopeActive(true);
    }

    public void CloseScope()
    {
        SetScopeActive(false);
    }

    public void SetScopeActive(bool active)
    {
        if (active && !CanOpenScopeNow())
            active = false;

        if (scopeActive == active)
        {
            ApplyScopeVisuals();
            return;
        }

        scopeActive = active;
        ApplyScopeVisuals();

        if (logState)
            Debug.Log($"[SniperScopeSwitch] Scope Active = {scopeActive}", this);
    }

    private bool ShouldForceCloseScopeNow()
    {
        if (closeWhenLeavingTopDown && !IsTopDownAllowedNow())
            return true;

        if (closeWhenStopAiming && !IsAimingAllowedNow())
            return true;

        return false;
    }

    private bool CanOpenScopeNow()
    {
        if (!IsTopDownAllowedNow())
            return false;

        if (!IsAimingAllowedNow())
            return false;

        return true;
    }

    private bool CanKeepScopeOpenNow()
    {
        if (!IsTopDownAllowedNow())
            return false;

        if (!IsAimingAllowedNow())
            return false;

        return true;
    }

    private bool IsTopDownAllowedNow()
    {
        if (!onlyAllowInTopDown)
            return true;

        if (switchCamView == null)
            return false;

        return switchCamView.IsTopDown;
    }

    private bool IsAimingAllowedNow()
    {
        if (!requireAiming)
            return true;

        if (playerAimSettings == null)
            return false;

        return playerAimSettings.IsAiming;
    }

    private bool ShouldShowCrosshairNow(bool showScope)
    {
        if (hideCrosshairWhenScopeActive && showScope)
            return false;

        if (showCrosshairOnlyInTopDown)
        {
            if (switchCamView == null)
                return false;

            if (!switchCamView.IsTopDown)
                return false;
        }

        if (showCrosshairOnlyWhenAiming && !IsAimingAllowedNow())
            return false;

        return true;
    }

    private void ApplyScopeVisuals()
    {
        bool showScope =
            scopeActive &&
            IsTopDownAllowedNow() &&
            IsAimingAllowedNow();

        if (topDownScopeUI != null && topDownScopeUI.activeSelf != showScope)
            topDownScopeUI.SetActive(showScope);

        if (topDownCrosshairUI != null)
        {
            bool showCrosshair = ShouldShowCrosshairNow(showScope);

            if (topDownCrosshairUI.activeSelf != showCrosshair)
                topDownCrosshairUI.SetActive(showCrosshair);
        }
    }
}
