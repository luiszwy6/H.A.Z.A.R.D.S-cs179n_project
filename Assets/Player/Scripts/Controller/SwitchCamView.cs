using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class SwitchCamView : MonoBehaviour
{
    public enum CameraViewMode
    {
        TopDown,
        ThirdPerson,
        FirstPerson
    }

    [Header("Start Mode")]
    [SerializeField] private CameraViewMode startMode = CameraViewMode.TopDown;

    [Header("Input")]
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private string switchViewActionName = "SwitchView";
    [SerializeField] private Key fallbackKey = Key.V;

    [Header("Core")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerWeaponSlots playerWeaponSlots;
    [SerializeField] private Animator animator;

    [Header("Views")]
    [SerializeField] private PlayerTopDownView topDownView;
    [SerializeField] private PlayerTpsView tpsView;
    [SerializeField] private PlayerFpsView fpsView;

    [Header("Camera Components")]
    [SerializeField] private Behaviour topDownCamera;
    [SerializeField] private Behaviour tpsCamera;
    [SerializeField] private Behaviour fpsCamera;

    [Header("View Models")]
    [SerializeField] private GameObject thirdPersonModel;
    [SerializeField] private GameObject firstPersonModel;
    [SerializeField] private bool switchViewModels = true;

    [Header("Canvas Crosshair UI")]
    [SerializeField] private bool switchCanvasCrosshairUI = true;
    [SerializeField] private GameObject topDownCrosshairUI;
    [SerializeField] private GameObject tpsCrosshairUI;
    [SerializeField] private GameObject fpsCrosshairUI;

    [Header("Sniper Aim UI")]
    [SerializeField] private GameObject sniperAimUI;
    [SerializeField] private bool hideTpsUIWhenSniperActive = true;

    [Header("Optional Top Down")]
    [SerializeField] private PlayerAimSettings topDownAimSettings;
    [SerializeField] private PlayerCrossHairSettings[] topDownCrosshairs;

    [Header("TPS Snap From Top Down Aim")]
    [SerializeField] private bool snapTpsToTopDownAimOnSwitch = false;
    [SerializeField] private bool onlySnapWhenTopDownAiming = true;
    [SerializeField] private bool refineTpsSnapAfterCameraUpdate = true;
    [SerializeField] private int refineTpsSnapPasses = 2;

    [Header("Top Down Cursor Restore")]
    [SerializeField] private bool restoreTopDownCursorOnReturn = true;
    [SerializeField] private int restoreTopDownCursorFrames = 2;
    [SerializeField] private bool unlockCursorOnReturnToTopDown = true;
    [SerializeField] private bool showCursorOnReturnToTopDown = true;
    [SerializeField] private bool clampRestoredCursorToScreen = true;

    [Header("Switch Cancel")]
    [SerializeField] private bool cancelAimOnSwitch = true;
    [SerializeField] private bool cancelShootingOnSwitch = true;
    [SerializeField] private bool clearAnimatorCombatStateOnSwitch = true;

    [Header("Animator Params")]
    [SerializeField] private string isAimingBoolName = "IsAiming";
    [SerializeField] private string shootTriggerName = "Shoot";
    [SerializeField] private string isShootingBoolName = "IsShooting";
    [SerializeField] private string keepShootingBoolName = "KeepShooting";
    [SerializeField] private string quickShotBoolName = "QuickShot";

    private InputAction switchViewAction;
    private CameraViewMode currentMode;
    private Coroutine tpsSnapRefineRoutine;
    private Coroutine topDownCursorRestoreRoutine;

    private bool hasStoredTopDownAimPoint;
    private Vector3 storedTopDownAimPoint;

    private int isAimingBoolHash;
    private int shootTriggerHash;
    private int isShootingBoolHash;
    private int keepShootingBoolHash;
    private int quickShotBoolHash;

    public CameraViewMode CurrentMode => currentMode;
    public bool IsTopDown => currentMode == CameraViewMode.TopDown;
    public bool IsThirdPerson => currentMode == CameraViewMode.ThirdPerson;
    public bool IsFirstPerson => currentMode == CameraViewMode.FirstPerson;

    private void Reset()
    {
        playerInput = GetComponent<PlayerInput>();
        playerMovement = GetComponent<PlayerMovement>();
        playerWeaponSlots = GetComponent<PlayerWeaponSlots>();
        topDownView = GetComponent<PlayerTopDownView>();
        tpsView = GetComponent<PlayerTpsView>();
        fpsView = GetComponent<PlayerFpsView>();
        topDownAimSettings = GetComponent<PlayerAimSettings>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    private void Awake()
    {
        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();

        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();

        if (playerWeaponSlots == null)
            playerWeaponSlots = GetComponent<PlayerWeaponSlots>();

        if (topDownView == null)
            topDownView = GetComponent<PlayerTopDownView>();

        if (tpsView == null)
            tpsView = GetComponent<PlayerTpsView>();

        if (fpsView == null)
            fpsView = GetComponent<PlayerFpsView>();

        if (topDownAimSettings == null)
            topDownAimSettings = GetComponent<PlayerAimSettings>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        isAimingBoolHash = Animator.StringToHash(isAimingBoolName);
        shootTriggerHash = Animator.StringToHash(shootTriggerName);
        isShootingBoolHash = Animator.StringToHash(isShootingBoolName);
        keepShootingBoolHash = Animator.StringToHash(keepShootingBoolName);
        quickShotBoolHash = Animator.StringToHash(quickShotBoolName);

        CacheInputAction();
        SetViewMode(startMode, false);
    }

    private void OnEnable()
    {
        CacheInputAction();
        switchViewAction?.Enable();
        SetViewMode(currentMode, false);
    }

    private void OnDisable()
    {
        switchViewAction?.Disable();
        StopTpsSnapRefine();
        StopTopDownCursorRestore();
    }

    private void Update()
    {
        bool switchPressed = false;

        if (switchViewAction != null && switchViewAction.WasPressedThisFrame())
            switchPressed = true;

        if (!switchPressed && Keyboard.current != null && Keyboard.current[fallbackKey].wasPressedThisFrame)
            switchPressed = true;

        if (switchPressed)
            ToggleViewMode();
    }

    public void ToggleViewMode()
    {
        if (currentMode == CameraViewMode.TopDown)
            SetViewMode(CameraViewMode.ThirdPerson, true);
        else
            SetViewMode(CameraViewMode.TopDown, true);
    }

    public void SetTopDownMode()
    {
        SetViewMode(CameraViewMode.TopDown, true);
    }

    public void SetThirdPersonMode()
    {
        SetViewMode(CameraViewMode.ThirdPerson, true);
    }

    public void SetFirstPersonMode()
    {
        SetViewMode(CameraViewMode.FirstPerson, true);
    }

    public void SetViewMode(CameraViewMode mode)
    {
        SetViewMode(mode, true);
    }

    public void SetViewMode(CameraViewMode mode, bool cancelRuntimeState)
    {
        bool modeChanged = currentMode != mode;

        bool switchingTopDownToTps =
            currentMode == CameraViewMode.TopDown &&
            mode == CameraViewMode.ThirdPerson;

        bool switchingAwayFromTopDown =
            currentMode == CameraViewMode.TopDown &&
            mode != CameraViewMode.TopDown;

        bool switchingBackToTopDown =
            currentMode != CameraViewMode.TopDown &&
            mode == CameraViewMode.TopDown;

        if (switchingAwayFromTopDown)
            StoreCurrentTopDownAimPoint();

        Vector3 topDownAimSnapPoint = Vector3.zero;

        bool shouldSnapTpsToAim =
            snapTpsToTopDownAimOnSwitch &&
            switchingTopDownToTps &&
            TryGetTopDownAimSnapPoint(out topDownAimSnapPoint);

        if (modeChanged && cancelRuntimeState)
            CancelRuntimeStateForViewSwitch();

        currentMode = mode;

        bool topDownActive = mode == CameraViewMode.TopDown;
        bool tpsActive = mode == CameraViewMode.ThirdPerson;
        bool fpsActive = mode == CameraViewMode.FirstPerson;

        if (!tpsActive)
            StopTpsSnapRefine();

        if (!topDownActive)
            StopTopDownCursorRestore();

        if (topDownView != null)
            topDownView.enabled = topDownActive;

        if (tpsView != null)
            tpsView.enabled = tpsActive;

        if (fpsView != null)
            fpsView.enabled = fpsActive;

        if (playerMovement != null)
        {
            if (topDownActive && topDownView != null)
                playerMovement.SetActiveView(topDownView);

            if (tpsActive && tpsView != null)
                playerMovement.SetActiveView(tpsView);

            if (fpsActive && fpsView != null)
                playerMovement.SetActiveView(fpsView);
        }

        if (shouldSnapTpsToAim && tpsView != null)
        {
            tpsView.SnapViewToWorldPoint(topDownAimSnapPoint);
            StartTpsSnapRefine(topDownAimSnapPoint);
        }

        if (topDownCamera != null)
            topDownCamera.enabled = topDownActive;

        if (tpsCamera != null)
            tpsCamera.enabled = tpsActive;

        if (fpsCamera != null)
            fpsCamera.enabled = fpsActive;

        if (topDownAimSettings != null)
            topDownAimSettings.SetExternalAimOverride(false);

        if (!tpsActive)
            HideSniperAimUI();

        SetTopDownCrosshairsVisible(topDownActive);
        ApplyCanvasCrosshairUI(topDownActive, tpsActive, fpsActive);
        ApplyViewModels(fpsActive);

        if (switchingBackToTopDown)
            StartTopDownCursorRestore();
    }

    private void CancelRuntimeStateForViewSwitch()
    {
        if (cancelAimOnSwitch)
            CancelAimForViewSwitch();

        if (cancelShootingOnSwitch)
            CancelShootingForViewSwitch();

        if (clearAnimatorCombatStateOnSwitch)
            ClearAnimatorCombatState();
    }

    private void CancelAimForViewSwitch()
    {
        if (topDownAimSettings != null)
            topDownAimSettings.CancelAimAndRequireRepress();

        if (topDownView != null)
            topDownView.CancelAimAndRequireRepress();

        if (tpsView != null)
            tpsView.CancelAimAndRequireRepress();

        if (fpsView != null)
            fpsView.CancelAimAndRequireRepress();
    }

    private void CancelShootingForViewSwitch()
    {
        ARShootSettings[] arShootSettings = GetComponentsInChildren<ARShootSettings>(true);

        for (int i = 0; i < arShootSettings.Length; i++)
        {
            if (arShootSettings[i] == null)
                continue;

            arShootSettings[i].ForceClearRuntimeState();
        }

        SG_ShootSettings[] sgShootSettings = GetComponentsInChildren<SG_ShootSettings>(true);

        for (int i = 0; i < sgShootSettings.Length; i++)
        {
            if (sgShootSettings[i] == null)
                continue;

            sgShootSettings[i].ForceClearRuntimeState();
        }
    }

    private void ClearAnimatorCombatState()
    {
        if (animator == null)
            return;

        if (!string.IsNullOrWhiteSpace(shootTriggerName))
            animator.ResetTrigger(shootTriggerHash);

        SetAnimatorBoolIfExists(isAimingBoolHash, false);
        SetAnimatorBoolIfExists(isShootingBoolHash, false);
        SetAnimatorBoolIfExists(keepShootingBoolHash, false);
        SetAnimatorBoolIfExists(quickShotBoolHash, false);
    }

    private void SetAnimatorBoolIfExists(int parameterHash, bool value)
    {
        if (animator == null)
            return;

        AnimatorControllerParameter[] parameters = animator.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].nameHash != parameterHash)
                continue;

            if (parameters[i].type != AnimatorControllerParameterType.Bool)
                return;

            animator.SetBool(parameterHash, value);
            return;
        }
    }

    public void SetSniperAimUI(bool active)
    {
        SetCrosshairUIActive(sniperAimUI, active);

        if (hideTpsUIWhenSniperActive)
            SetCrosshairUIActive(tpsCrosshairUI, !active);
    }

    private void HideSniperAimUI()
    {
        SetCrosshairUIActive(sniperAimUI, false);
    }

    private void ApplyViewModels(bool fpsActive)
    {
        if (!switchViewModels)
            return;

        if (firstPersonModel != null)
            firstPersonModel.SetActive(fpsActive);

        if (thirdPersonModel != null)
            thirdPersonModel.SetActive(!fpsActive);
    }

    private void ApplyCanvasCrosshairUI(bool topDownActive, bool tpsActive, bool fpsActive)
    {
        if (!switchCanvasCrosshairUI)
            return;

        SetCrosshairUIActive(topDownCrosshairUI, topDownActive);
        SetCrosshairUIActive(tpsCrosshairUI, tpsActive);
        SetCrosshairUIActive(fpsCrosshairUI, fpsActive);
    }

    private void SetCrosshairUIActive(GameObject crosshairObject, bool active)
    {
        if (crosshairObject == null)
            return;

        if (crosshairObject.activeSelf != active)
            crosshairObject.SetActive(active);
    }

    private void StoreCurrentTopDownAimPoint()
    {
        if (topDownAimSettings == null)
            return;

        if (!topDownAimSettings.IsAiming)
            return;

        storedTopDownAimPoint = topDownAimSettings.AimPointClamped;
        hasStoredTopDownAimPoint = true;
    }

    private bool TryGetTopDownAimSnapPoint(out Vector3 point)
    {
        point = Vector3.zero;

        if (topDownAimSettings == null)
            return false;

        if (onlySnapWhenTopDownAiming && !topDownAimSettings.IsAiming)
            return false;

        point = topDownAimSettings.AimPointClamped;
        return true;
    }

    private void StartTpsSnapRefine(Vector3 snapPoint)
    {
        if (!refineTpsSnapAfterCameraUpdate)
            return;

        StopTpsSnapRefine();

        tpsSnapRefineRoutine = StartCoroutine(TpsSnapRefineRoutine(snapPoint));
    }

    private void StopTpsSnapRefine()
    {
        if (tpsSnapRefineRoutine == null)
            return;

        StopCoroutine(tpsSnapRefineRoutine);
        tpsSnapRefineRoutine = null;
    }

    private IEnumerator TpsSnapRefineRoutine(Vector3 snapPoint)
    {
        int passes = Mathf.Max(1, refineTpsSnapPasses);

        for (int i = 0; i < passes; i++)
        {
            yield return null;
            yield return new WaitForEndOfFrame();

            if (currentMode != CameraViewMode.ThirdPerson)
                break;

            if (tpsView != null)
                tpsView.RefineSnapViewToWorldPoint(snapPoint);
        }

        tpsSnapRefineRoutine = null;
    }

    private void StartTopDownCursorRestore()
    {
        if (!restoreTopDownCursorOnReturn)
            return;

        if (!hasStoredTopDownAimPoint)
            return;

        StopTopDownCursorRestore();

        topDownCursorRestoreRoutine = StartCoroutine(
            RestoreTopDownCursorRoutine(storedTopDownAimPoint)
        );
    }

    private void StopTopDownCursorRestore()
    {
        if (topDownCursorRestoreRoutine == null)
            return;

        StopCoroutine(topDownCursorRestoreRoutine);
        topDownCursorRestoreRoutine = null;
    }

    private IEnumerator RestoreTopDownCursorRoutine(Vector3 worldPoint)
    {
        int frames = Mathf.Max(1, restoreTopDownCursorFrames);

        for (int i = 0; i < frames; i++)
        {
            yield return null;
            yield return new WaitForEndOfFrame();

            if (currentMode != CameraViewMode.TopDown)
                break;

            RestoreCursorToTopDownWorldPoint(worldPoint);
        }

        topDownCursorRestoreRoutine = null;
    }

    private void RestoreCursorToTopDownWorldPoint(Vector3 worldPoint)
    {
        if (Mouse.current == null)
            return;

        Camera cam = null;

        if (topDownAimSettings != null && topDownAimSettings.aimCamera != null)
            cam = topDownAimSettings.aimCamera;
        else
            cam = Camera.main;

        if (cam == null)
            return;

        Vector3 screenPoint = cam.WorldToScreenPoint(worldPoint);

        if (screenPoint.z < 0f)
            return;

        Vector2 cursorPosition = new Vector2(screenPoint.x, screenPoint.y);

        if (clampRestoredCursorToScreen)
        {
            cursorPosition.x = Mathf.Clamp(cursorPosition.x, 0f, Screen.width);
            cursorPosition.y = Mathf.Clamp(cursorPosition.y, 0f, Screen.height);
        }

        if (unlockCursorOnReturnToTopDown)
            Cursor.lockState = CursorLockMode.None;

        Cursor.visible = showCursorOnReturnToTopDown;

        Mouse.current.WarpCursorPosition(cursorPosition);
    }

    private void CacheInputAction()
    {
        if (playerInput == null || playerInput.actions == null)
            return;

        if (string.IsNullOrWhiteSpace(switchViewActionName))
            return;

        switchViewAction = playerInput.actions.FindAction(switchViewActionName, false);
    }

    private void SetTopDownCrosshairsVisible(bool visible)
    {
        if (topDownCrosshairs != null)
        {
            for (int i = 0; i < topDownCrosshairs.Length; i++)
            {
                if (topDownCrosshairs[i] == null)
                    continue;

                topDownCrosshairs[i].forceHideCrosshair = !visible;
            }
        }

        PlayerCrossHairSettings currentCrosshair = GetCurrentWeaponCrosshair();

        if (currentCrosshair != null)
            currentCrosshair.forceHideCrosshair = !visible;
    }

    private PlayerCrossHairSettings GetCurrentWeaponCrosshair()
    {
        if (playerWeaponSlots == null)
            return null;

        return playerWeaponSlots.CurrentCrossHairSettings;
    }
}