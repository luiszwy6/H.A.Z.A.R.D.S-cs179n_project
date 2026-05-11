using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInput))]
public class PlayerFpsView : PlayerMovementViewBase
{
    [Header("Input Action Names")]
    [SerializeField] private string lookActionName = "Look";
    [SerializeField] private string aimActionName = "Aim";

    [Header("Refs")]
    [SerializeField] private PlayerInput playerInput;

    [Header("Camera Target")]
    [SerializeField] private Transform cameraTarget;
    [SerializeField] private Transform cameraFollowTarget;
    [SerializeField] private Vector3 cameraTargetOffset = new Vector3(0f, 1.65f, 0f);
    [SerializeField] private bool updateCameraTargetTransform = true;

    [Header("Camera")]
    [SerializeField] private Camera aimCamera;
    [SerializeField] private Transform cameraTransform;

    [Header("Aim")]
    [SerializeField] private float aimDistance = 150f;
    [SerializeField] private float fallbackAimDistance = 80f;
    [SerializeField] private LayerMask aimLayers = ~0;
    [SerializeField] private Vector2 screenCenterOffset = Vector2.zero;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("Look Sensitivity")]
    [SerializeField] private float mouseSensitivity = 0.12f;
    [SerializeField] private float gamepadSensitivity = 180f;
    [SerializeField] private bool invertY = false;

    [Header("Pitch Clamp")]
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;

    [Header("Cursor")]
    [SerializeField] private bool lockCursorOnEnable = true;
    [SerializeField] private bool unlockCursorOnDisable = true;

    public Vector2 LookInput { get; private set; }
    public bool IsAimHeld { get; private set; }
    public bool AimPressedThisFrame { get; private set; }

    public float Yaw => yaw;
    public float Pitch => pitch;

    public override bool IsViewAiming => IsAimHeld;
    public override bool HasViewAimPoint => hasAimPoint;
    public override Vector3 ViewAimPoint => aimPoint;
    public override Vector3 ViewAimWorldDir => GetYawForward();

    private InputAction lookAction;
    private InputAction aimAction;

    private float yaw;
    private float pitch;

    private Vector3 aimPoint;
    private bool hasAimPoint;

    private bool externalAimOverride;
    private bool externalAimPressedPulse;
    private bool inputAimSuppressedUntilRelease;

    private void Reset()
    {
        playerInput = GetComponent<PlayerInput>();

        if (cameraFollowTarget == null)
            cameraFollowTarget = transform;

        if (aimCamera == null)
            aimCamera = Camera.main;

        if (aimCamera != null)
            cameraTransform = aimCamera.transform;
    }

    private void Awake()
    {
        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();

        if (cameraFollowTarget == null)
            cameraFollowTarget = transform;

        if (aimCamera == null)
            aimCamera = Camera.main;

        if (cameraTransform == null && aimCamera != null)
            cameraTransform = aimCamera.transform;

        CacheInputActions();
        InitializeCameraAngles();
    }

    private void OnEnable()
    {
        CacheInputActions();
        InitializeCameraAngles();
        ApplyCameraTargetTransform();

        if (lockCursorOnEnable)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void OnDisable()
    {
        LookInput = Vector2.zero;
        IsAimHeld = false;
        AimPressedThisFrame = false;

        hasAimPoint = false;
        aimPoint = transform.position;

        externalAimOverride = false;
        externalAimPressedPulse = false;
        inputAimSuppressedUntilRelease = false;

        if (unlockCursorOnDisable)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void LateUpdate()
    {
        ApplyCameraTargetTransform();
    }

    public override void ResetView(Transform actor)
    {
        InitializeCameraAngles();
        ApplyCameraTargetTransform();
    }

    public override PlayerMovementViewFrame BuildViewFrame(
        Transform actor,
        Vector2 moveInput,
        Vector3 fallbackMoveDirWorld,
        float inputMagnitude,
        bool wantsToMove,
        bool isCrouchingOrProne)
    {
        ReadInput();
        UpdateLookAngles(Time.deltaTime);
        UpdateAimPoint(actor);

        Vector3 moveDirWorld = GetYawRelativeMoveDirection(moveInput);

        if (!wantsToMove)
            moveDirWorld = Vector3.zero;

        Vector3 facingDir = GetYawForward();

        return new PlayerMovementViewFrame(
            moveDirWorld,
            facingDir,
            inputMagnitude,
            wantsToMove,
            IsAimHeld,
            AimPressedThisFrame
        );
    }

    public override bool TryGetMuzzleAimPoint(out Vector3 point)
    {
        point = Vector3.zero;

        if (!IsAimHeld && !externalAimOverride)
            return false;

        if (hasAimPoint)
        {
            point = aimPoint;
            return true;
        }

        if (aimCamera != null)
        {
            Vector2 screenPoint = new Vector2(
                Screen.width * 0.5f + screenCenterOffset.x,
                Screen.height * 0.5f + screenCenterOffset.y
            );

            Ray ray = aimCamera.ScreenPointToRay(screenPoint);
            point = ray.origin + ray.direction * fallbackAimDistance;
            return true;
        }

        if (cameraTransform != null)
        {
            point = cameraTransform.position + cameraTransform.forward * fallbackAimDistance;
            return true;
        }

        return false;
    }

    public override bool TryGetViewShotRay(out Ray shotRay)
    {
        shotRay = default;

        if (!IsAimHeld && !externalAimOverride)
            return false;

        if (aimCamera != null)
        {
            Vector2 screenPoint = new Vector2(
                Screen.width * 0.5f + screenCenterOffset.x,
                Screen.height * 0.5f + screenCenterOffset.y
            );

            shotRay = aimCamera.ScreenPointToRay(screenPoint);
            return true;
        }

        if (cameraTransform != null)
        {
            shotRay = new Ray(cameraTransform.position, cameraTransform.forward);
            return true;
        }

        return false;
    }

    public override void SetExternalAimOverride(bool active)
    {
        bool wasActive = externalAimOverride;

        externalAimOverride = active;

        if (active && !wasActive)
            externalAimPressedPulse = true;

        if (active)
            IsAimHeld = true;
        else if (aimAction == null || !aimAction.IsPressed())
            IsAimHeld = false;
    }

    public override void SetExternalAimOverride(bool active, float rotationSpeedOverride)
    {
        SetExternalAimOverride(active);
    }

    public override void CancelAimRuntimeOnly()
    {
        externalAimOverride = false;
        externalAimPressedPulse = false;

        IsAimHeld = false;
        AimPressedThisFrame = false;

        hasAimPoint = false;
        aimPoint = transform.position;

        inputAimSuppressedUntilRelease = false;
    }

    public override void CancelAimAndRequireRepress()
    {
        externalAimOverride = false;
        externalAimPressedPulse = false;

        IsAimHeld = false;
        AimPressedThisFrame = false;

        hasAimPoint = false;
        aimPoint = transform.position;

        if (aimAction != null && aimAction.IsPressed())
            inputAimSuppressedUntilRelease = true;
        else
            inputAimSuppressedUntilRelease = false;
    }

    private void CacheInputActions()
    {
        if (playerInput == null || playerInput.actions == null)
            return;

        lookAction = playerInput.actions.FindAction(lookActionName, false);
        aimAction = playerInput.actions.FindAction(aimActionName, false);
    }

    private void InitializeCameraAngles()
    {
        Transform source = cameraTarget != null ? cameraTarget : transform;

        Vector3 angles = source.eulerAngles;

        yaw = angles.y;
        pitch = PlayerMovementViewUtility.NormalizeAngle(angles.x);
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    private void ReadInput()
    {
        LookInput = lookAction != null
            ? lookAction.ReadValue<Vector2>()
            : Vector2.zero;

        bool rawAimHeld = aimAction != null && aimAction.IsPressed();

        if (!rawAimHeld)
            inputAimSuppressedUntilRelease = false;

        bool inputAimHeld = rawAimHeld && !inputAimSuppressedUntilRelease;
        bool inputAimPressedThisFrame =
            aimAction != null &&
            aimAction.WasPressedThisFrame() &&
            !inputAimSuppressedUntilRelease;

        IsAimHeld = inputAimHeld || externalAimOverride;
        AimPressedThisFrame = inputAimPressedThisFrame || externalAimPressedPulse;

        externalAimPressedPulse = false;
    }

    private void UpdateLookAngles(float dt)
    {
        if (LookInput.sqrMagnitude <= 0.000001f)
            return;

        bool usingMouse = playerInput != null &&
                          playerInput.currentControlScheme != null &&
                          playerInput.currentControlScheme.Contains("Mouse");

        float ySign = invertY ? 1f : -1f;

        if (usingMouse)
        {
            yaw += LookInput.x * mouseSensitivity;
            pitch += LookInput.y * mouseSensitivity * ySign;
        }
        else
        {
            yaw += LookInput.x * gamepadSensitivity * dt;
            pitch += LookInput.y * gamepadSensitivity * dt * ySign;
        }

        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    private void UpdateAimPoint(Transform actor)
    {
        hasAimPoint = false;
        aimPoint = actor.position + GetYawForward() * fallbackAimDistance;

        if (!IsAimHeld && !externalAimOverride)
            return;

        if (aimCamera == null)
        {
            if (cameraTransform != null)
                aimPoint = cameraTransform.position + cameraTransform.forward * fallbackAimDistance;

            hasAimPoint = true;
            return;
        }

        Vector2 screenPoint = new Vector2(
            Screen.width * 0.5f + screenCenterOffset.x,
            Screen.height * 0.5f + screenCenterOffset.y
        );

        Ray ray = aimCamera.ScreenPointToRay(screenPoint);

        if (Physics.Raycast(
            ray,
            out RaycastHit hit,
            aimDistance,
            aimLayers,
            triggerInteraction))
        {
            aimPoint = hit.point;
            hasAimPoint = true;
            return;
        }

        aimPoint = ray.origin + ray.direction * fallbackAimDistance;
        hasAimPoint = true;
    }

    private Vector3 GetYawRelativeMoveDirection(Vector2 moveInput)
    {
        if (moveInput.sqrMagnitude <= 0.0001f)
            return Vector3.zero;

        Quaternion yawRotation = Quaternion.Euler(0f, yaw, 0f);

        Vector3 forward = yawRotation * Vector3.forward;
        Vector3 right = yawRotation * Vector3.right;

        Vector3 moveDir = forward * moveInput.y + right * moveInput.x;
        moveDir.y = 0f;

        return moveDir.sqrMagnitude > 0.0001f
            ? moveDir.normalized
            : Vector3.zero;
    }

    private Vector3 GetYawForward()
    {
        Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        forward.y = 0f;

        return forward.sqrMagnitude > 0.0001f
            ? forward.normalized
            : transform.forward;
    }

    private void ApplyCameraTargetTransform()
    {
        if (!updateCameraTargetTransform)
            return;

        if (cameraTarget == null)
            return;

        Transform follow = cameraFollowTarget != null ? cameraFollowTarget : transform;
        cameraTarget.position = follow.position + cameraTargetOffset;
        cameraTarget.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }
}