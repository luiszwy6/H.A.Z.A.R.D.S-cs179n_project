using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInput))]
public class PlayerTpsView : PlayerMovementViewBase
{
    [Header("Input Action Names")]
    [SerializeField] private string lookActionName = "Look";
    [SerializeField] private string aimActionName = "Aim";

    [Header("Refs")]
    [SerializeField] private PlayerInput playerInput;

    [Header("Camera Target")]
    [SerializeField] private Transform cameraTarget;
    [SerializeField] private Transform cameraFollowTarget;
    [SerializeField] private Vector3 cameraTargetOffset = new Vector3(0f, 1.6f, 0f);
    [SerializeField] private bool updateCameraTargetTransform = true;
    [SerializeField] private float cameraTargetFollowSpeed = 0f;

    [Header("Aim Camera")]
    [SerializeField] private Camera aimCamera;
    [SerializeField] private Transform cameraTransform;

    [Header("Cinemachine Third Person Aim")]
    [SerializeField] private bool useCinemachineThirdPersonAim = true;
    [SerializeField] private MonoBehaviour cinemachineThirdPersonAimComponent;

    [Header("TPS Movement")]
    [SerializeField] private bool useCameraYawForMovement = true;

    [Header("TPS Aim Fallback")]
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
    [SerializeField] private float minPitch = -35f;
    [SerializeField] private float maxPitch = 70f;

    [Header("Cursor")]
    [SerializeField] private bool lockCursorOnEnable = true;
    [SerializeField] private bool unlockCursorOnDisable = true;

    public Vector2 LookInput { get; private set; }
    public bool IsAimHeld { get; private set; }
    public bool AimPressedThisFrame { get; private set; }
    public bool IsRealAimHeld { get; private set; }
    public bool IsRealAimPressedThisFrame { get; private set; }

    public override bool IsViewAiming => IsAimHeld;
    public override bool HasViewAimPoint => hasAimPoint;
    public override Vector3 ViewAimPoint => aimPointClamped;
    public override Vector3 ViewAimWorldDir => aimWorldDir;

    public float Yaw => yaw;
    public float Pitch => pitch;

    private InputAction lookAction;
    private InputAction aimAction;

    private float yaw;
    private float pitch;

    private Vector3 aimPointClamped;
    private Vector3 aimWorldDir;
    private bool hasAimPoint;
    private Ray aimRay;

    private MonoBehaviour cachedCinemachineAimComponent;
    private PropertyInfo cinemachineAimTargetProperty;
    private FieldInfo cinemachineAimTargetField;

    private bool externalAimOverride;
    private bool externalAimPressedPulse;

    private bool externalAimRotationSpeedOverrideActive;
    private float externalAimRotationSpeedOverride;
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
        CacheCinemachineAimTargetAccess();
        InitializeCameraAngles();
    }

    private void OnEnable()
    {
        CacheInputActions();
        CacheCinemachineAimTargetAccess();

        InitializeCameraAngles();
        ApplyCameraTargetTransform(true);

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
        IsRealAimHeld = false;
        IsRealAimPressedThisFrame = false;

        hasAimPoint = false;
        aimPointClamped = transform.position;
        aimWorldDir = transform.forward;

        externalAimOverride = false;
        externalAimPressedPulse = false;
        externalAimRotationSpeedOverrideActive = false;
        externalAimRotationSpeedOverride = 0f;
        inputAimSuppressedUntilRelease = false;

        if (unlockCursorOnDisable)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void LateUpdate()
    {
        ApplyCameraTargetTransform(false);
    }

    public override void ResetView(Transform actor)
    {
        InitializeCameraAngles();
        ApplyCameraTargetTransform(true);
    }

    public void SnapViewToWorldPoint(Vector3 worldPoint)
{
    Transform source = cameraTarget != null ? cameraTarget : transform;
    SnapViewToWorldPointFromSource(worldPoint, source);
}

public void RefineSnapViewToWorldPoint(Vector3 worldPoint)
{
    Transform source = cameraTransform != null ? cameraTransform : cameraTarget;

    if (source == null)
        source = transform;

    SnapViewToWorldPointFromSource(worldPoint, source);
}

private void SnapViewToWorldPointFromSource(Vector3 worldPoint, Transform source)
{
    if (source == null)
        return;

    Vector3 dir = worldPoint - source.position;

    if (dir.sqrMagnitude <= 0.0001f)
        return;

    Vector3 flatDir = dir;
    flatDir.y = 0f;

    float flatDistance = flatDir.magnitude;

    if (flatDistance <= 0.0001f)
        return;

    float newYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
    float newPitch = -Mathf.Atan2(dir.y, flatDistance) * Mathf.Rad2Deg;

    yaw = newYaw;
    pitch = Mathf.Clamp(newPitch, minPitch, maxPitch);

    ApplyCameraTargetTransform(true);
}

    public override bool TryGetMuzzleAimPoint(out Vector3 aimPoint)
    {
        aimPoint = Vector3.zero;

        bool aimingNow = IsAimHeld || externalAimOverride;

        if (!aimingNow)
            return false;

        if (useCinemachineThirdPersonAim &&
            TryGetCinemachineAimTarget(out Vector3 cinemachineAimTarget))
        {
            aimPoint = cinemachineAimTarget;
            return true;
        }

        if (hasAimPoint)
        {
            aimPoint = aimPointClamped;
            return true;
        }

        if (aimCamera != null)
        {
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
                return true;
            }

            aimPoint = ray.origin + ray.direction * fallbackAimDistance;
            return true;
        }

        if (cameraTransform != null)
        {
            aimPoint = cameraTransform.position + cameraTransform.forward * fallbackAimDistance;
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

        Vector3 moveDirWorld = useCameraYawForMovement
            ? GetYawRelativeMoveDirection(moveInput)
            : fallbackMoveDirWorld;

        if (!wantsToMove)
            moveDirWorld = Vector3.zero;

        Vector3 facingDir;

        if (IsAimHeld && aimWorldDir.sqrMagnitude > 0.0001f)
        {
            facingDir = aimWorldDir;
        }
        else if (moveDirWorld.sqrMagnitude > 0.0001f)
        {
            facingDir = moveDirWorld;
        }
        else
        {
            facingDir = actor.forward;
        }

        return new PlayerMovementViewFrame(
            moveDirWorld,
            facingDir,
            inputMagnitude,
            wantsToMove,
            IsAimHeld,
            AimPressedThisFrame
        );
    }

    private void CacheInputActions()
    {
        if (playerInput == null || playerInput.actions == null)
            return;

        lookAction = playerInput.actions.FindAction(lookActionName, false);
        aimAction = playerInput.actions.FindAction(aimActionName, false);
    }

    private void CacheCinemachineAimTargetAccess()
    {
        cachedCinemachineAimComponent = cinemachineThirdPersonAimComponent;
        cinemachineAimTargetProperty = null;
        cinemachineAimTargetField = null;

        if (cachedCinemachineAimComponent == null)
            return;

        System.Type type = cachedCinemachineAimComponent.GetType();

        cinemachineAimTargetProperty = type.GetProperty(
            "AimTarget",
            BindingFlags.Instance | BindingFlags.Public
        );

        if (cinemachineAimTargetProperty != null &&
            cinemachineAimTargetProperty.PropertyType == typeof(Vector3))
        {
            return;
        }

        cinemachineAimTargetProperty = null;

        cinemachineAimTargetField = type.GetField(
            "AimTarget",
            BindingFlags.Instance | BindingFlags.Public
        );

        if (cinemachineAimTargetField != null &&
            cinemachineAimTargetField.FieldType == typeof(Vector3))
        {
            return;
        }

        cinemachineAimTargetField = null;
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

        bool rawInputAimHeld = aimAction != null && aimAction.IsPressed();

        if (!rawInputAimHeld)
            inputAimSuppressedUntilRelease = false;

        bool inputAimHeld = rawInputAimHeld && !inputAimSuppressedUntilRelease;
        bool inputAimPressedThisFrame =
            aimAction != null &&
            aimAction.WasPressedThisFrame() &&
            !inputAimSuppressedUntilRelease;

        IsRealAimHeld = inputAimHeld;
        IsRealAimPressedThisFrame = inputAimPressedThisFrame;

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
        aimPointClamped = actor.position;
        aimWorldDir = GetYawForward();

        if (!IsAimHeld)
            return;

        if (useCinemachineThirdPersonAim &&
            TryGetCinemachineAimTarget(out Vector3 cinemachineAimTarget))
        {
            SetAimPoint(actor, cinemachineAimTarget);
            return;
        }

        if (aimCamera == null)
        {
            Vector3 fallbackPoint;

            if (cameraTransform != null)
                fallbackPoint = cameraTransform.position + cameraTransform.forward * fallbackAimDistance;
            else
                fallbackPoint = actor.position + GetYawForward() * fallbackAimDistance;

            SetAimPoint(actor, fallbackPoint);
            return;
        }

        Vector2 screenPoint = new Vector2(
            Screen.width * 0.5f + screenCenterOffset.x,
            Screen.height * 0.5f + screenCenterOffset.y
        );

        aimRay = aimCamera.ScreenPointToRay(screenPoint);

        if (Physics.Raycast(
            aimRay,
            out RaycastHit hit,
            aimDistance,
            aimLayers,
            triggerInteraction))
        {
            SetAimPoint(actor, hit.point);
            return;
        }

        SetAimPoint(actor, aimRay.origin + aimRay.direction * fallbackAimDistance);
    }

    private bool TryGetCinemachineAimTarget(out Vector3 aimTarget)
    {
        aimTarget = Vector3.zero;

        if (cinemachineThirdPersonAimComponent == null)
            return false;

        if (cachedCinemachineAimComponent != cinemachineThirdPersonAimComponent)
            CacheCinemachineAimTargetAccess();

        if (cinemachineAimTargetProperty != null)
        {
            object value = cinemachineAimTargetProperty.GetValue(cinemachineThirdPersonAimComponent);

            if (value is Vector3 point)
            {
                aimTarget = point;
                return true;
            }
        }

        if (cinemachineAimTargetField != null)
        {
            object value = cinemachineAimTargetField.GetValue(cinemachineThirdPersonAimComponent);

            if (value is Vector3 point)
            {
                aimTarget = point;
                return true;
            }
        }

        return false;
    }

    private void SetAimPoint(Transform actor, Vector3 point)
    {
        aimPointClamped = point;
        hasAimPoint = true;

        Vector3 dir = GetYawForward();

        if (dir.sqrMagnitude <= 0.0001f)
        {
            dir = point - actor.position;
            dir.y = 0f;
        }

        if (dir.sqrMagnitude > 0.0001f)
        {
            dir.Normalize();
            aimWorldDir = dir;
        }
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

    private void ApplyCameraTargetTransform(bool snap)
    {
        if (!updateCameraTargetTransform)
            return;

        if (cameraTarget == null)
            return;

        Transform follow = cameraFollowTarget != null ? cameraFollowTarget : transform;
        Vector3 targetPos = follow.position + cameraTargetOffset;

        if (snap || cameraTargetFollowSpeed <= 0f)
        {
            cameraTarget.position = targetPos;
        }
        else
        {
            float t = 1f - Mathf.Exp(-cameraTargetFollowSpeed * Time.deltaTime);
            cameraTarget.position = Vector3.Lerp(cameraTarget.position, targetPos, t);
        }

        cameraTarget.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    public override void SetExternalAimOverride(bool active)
    {
        SetExternalAimOverrideInternal(active, false, 0f);
    }

    public override void SetExternalAimOverride(bool active, float rotationSpeedOverride)
    {
        SetExternalAimOverrideInternal(active, true, rotationSpeedOverride);
    }

    private void SetExternalAimOverrideInternal(bool active, bool useRotationSpeedOverride, float rotationSpeedOverride)
    {
        bool wasActive = externalAimOverride;

        externalAimOverride = active;

        if (active && !wasActive)
            externalAimPressedPulse = true;

        if (active)
        {
            if (useRotationSpeedOverride)
            {
                externalAimRotationSpeedOverrideActive = true;
                externalAimRotationSpeedOverride = Mathf.Max(0f, rotationSpeedOverride);
            }
            else
            {
                externalAimRotationSpeedOverrideActive = false;
                externalAimRotationSpeedOverride = 0f;
            }

            IsAimHeld = true;
        }
        else
        {
            externalAimRotationSpeedOverrideActive = false;
            externalAimRotationSpeedOverride = 0f;

            if (aimAction == null || !aimAction.IsPressed())
                IsAimHeld = false;
        }
    }

    public override void CancelAimAndRequireRepress()
    {
        externalAimOverride = false;
        externalAimPressedPulse = false;
        externalAimRotationSpeedOverrideActive = false;
        externalAimRotationSpeedOverride = 0f;

        IsAimHeld = false;
        AimPressedThisFrame = false;
        IsRealAimHeld = false;
        IsRealAimPressedThisFrame = false;

        hasAimPoint = false;
        aimPointClamped = transform.position;
        aimWorldDir = transform.forward;

        inputAimSuppressedUntilRelease = false;
    }

    public override bool TryGetExternalAimRotationSpeedOverride(out float rotationSpeed)
    {
        rotationSpeed = externalAimRotationSpeedOverride;

        return externalAimOverride && externalAimRotationSpeedOverrideActive;
    }
}