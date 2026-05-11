using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class Player3rdPersonAim : MonoBehaviour
{
    [Header("Input Action Names")]
    [SerializeField] private string lookActionName = "Look";
    [SerializeField] private string aimActionName = "Aim";

    [Header("Camera References")]
    [SerializeField] private Transform cameraTarget;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Camera aimCamera;

    [Header("Camera Rotation")]
    [SerializeField] private float mouseSensitivity = 0.12f;
    [SerializeField] private float gamepadSensitivity = 180f;
    [SerializeField] private bool invertY = false;
    [SerializeField] private float minPitch = -35f;
    [SerializeField] private float maxPitch = 70f;
    [SerializeField] private bool rotateCameraOnlyWhenAiming = false;

    [Header("Third Person Aim")]
    [SerializeField] private bool aimOnlyWhenAimHeld = true;
    [SerializeField] private float aimDistance = 150f;
    [SerializeField] private LayerMask aimLayers = ~0;
    [SerializeField] private Vector2 screenCenterOffset = Vector2.zero;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("Fallback")]
    [SerializeField] private float fallbackAimDistance = 80f;

    public bool IsAiming { get; private set; }
    public bool IsAimHeld => aimHeldState;
    public bool AimPressedThisFrame => aimPressedThisFrameState;

    public bool IsAimInputHeld => aimAction != null && aimAction.IsPressed();
    public bool AimInputPressedThisFrame => aimAction != null && aimAction.WasPressedThisFrame();

    public Vector2 LookInput { get; private set; }
    public bool UsingMouseScheme { get; private set; }

    public Vector3 AimWorldDir { get; private set; }
    public Vector3 AimPointClamped { get; private set; }
    public bool HasAimPoint { get; private set; }
    public Ray AimRay { get; private set; }

    private PlayerInput playerInput;
    private InputAction lookAction;
    private InputAction aimAction;

    private float yaw;
    private float pitch;

    private bool externalAimOverride;
    private bool externalAimPressedPulse;
    private bool aimHeldState;
    private bool aimPressedThisFrameState;

    private void Reset()
    {
        if (aimCamera == null)
            aimCamera = Camera.main;

        if (aimCamera != null)
            cameraTransform = aimCamera.transform;
    }

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();

        if (aimCamera == null)
            aimCamera = Camera.main;

        if (cameraTransform == null && aimCamera != null)
            cameraTransform = aimCamera.transform;

        InitializeCameraAngles();
    }

    private void OnEnable()
    {
        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();

        if (playerInput != null && playerInput.actions != null)
        {
            lookAction = playerInput.actions[lookActionName];
            aimAction = playerInput.actions[aimActionName];
        }

        lookAction?.Enable();
        aimAction?.Enable();
    }

    private void OnDisable()
    {
        lookAction?.Disable();
        aimAction?.Disable();

        externalAimOverride = false;
        externalAimPressedPulse = false;
        aimHeldState = false;
        aimPressedThisFrameState = false;
        IsAiming = false;

        LookInput = Vector2.zero;
        AimWorldDir = Vector3.zero;
        AimPointClamped = transform.position;
        HasAimPoint = false;
    }

    private void InitializeCameraAngles()
    {
        Transform source = cameraTarget != null ? cameraTarget : transform;

        Vector3 angles = source.eulerAngles;
        yaw = angles.y;
        pitch = NormalizeAngle(angles.x);
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    public void SetExternalAimOverride(bool active)
    {
        bool wasActive = externalAimOverride;

        externalAimOverride = active;

        if (active && !wasActive)
            externalAimPressedPulse = true;
    }

    public Vector3 TickAimAndGetFacingDirection(Transform actor, Vector3 moveDirWorld)
    {
        if (actor == null)
            return moveDirWorld;

        ReadInputState();
        TickCameraRotation();

        UpdateThirdPersonAim(actor);

        if (IsAiming && AimWorldDir.sqrMagnitude > 0.0001f)
            return AimWorldDir;

        if (moveDirWorld.sqrMagnitude > 0.0001f)
            return moveDirWorld;

        Vector3 actorForward = actor.forward;
        actorForward.y = 0f;

        return actorForward.sqrMagnitude > 0.0001f
            ? actorForward.normalized
            : Vector3.forward;
    }

    public Vector3 GetShootDirection(Transform muzzlePoint)
    {
        if (muzzlePoint == null)
        {
            if (cameraTransform != null)
                return cameraTransform.forward;

            return transform.forward;
        }

        Vector3 dir = AimPointClamped - muzzlePoint.position;

        if (dir.sqrMagnitude > 0.0001f)
            return dir.normalized;

        if (cameraTransform != null)
            return cameraTransform.forward;

        return muzzlePoint.forward;
    }

    private void ReadInputState()
    {
        bool inputAimHeld = aimAction != null && aimAction.IsPressed();
        bool inputAimPressedThisFrame = aimAction != null && aimAction.WasPressedThisFrame();

        aimHeldState = inputAimHeld || externalAimOverride;
        aimPressedThisFrameState = inputAimPressedThisFrame || externalAimPressedPulse;
        externalAimPressedPulse = false;

        IsAiming = aimOnlyWhenAimHeld ? aimHeldState : true;

        LookInput = lookAction != null
            ? lookAction.ReadValue<Vector2>()
            : Vector2.zero;

        UsingMouseScheme = playerInput != null &&
                           playerInput.currentControlScheme != null &&
                           playerInput.currentControlScheme.Contains("Mouse");
    }

    private void TickCameraRotation()
    {
        if (cameraTarget == null)
            return;

        if (rotateCameraOnlyWhenAiming && !IsAiming)
            return;

        Vector2 look = LookInput;

        if (look.sqrMagnitude < 0.000001f)
            return;

        float ySign = invertY ? 1f : -1f;

        if (UsingMouseScheme)
        {
            yaw += look.x * mouseSensitivity;
            pitch += look.y * mouseSensitivity * ySign;
        }
        else
        {
            yaw += look.x * gamepadSensitivity * Time.deltaTime;
            pitch += look.y * gamepadSensitivity * Time.deltaTime * ySign;
        }

        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        cameraTarget.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void UpdateThirdPersonAim(Transform actor)
    {
        AimWorldDir = Vector3.zero;
        AimPointClamped = actor.position;
        HasAimPoint = false;

        if (aimCamera == null)
        {
            if (cameraTransform != null)
            {
                Vector3 fallbackPoint = cameraTransform.position + cameraTransform.forward * fallbackAimDistance;
                SetAimPoint(actor, fallbackPoint);
            }

            return;
        }

        Vector2 screenPoint = new Vector2(
            Screen.width * 0.5f + screenCenterOffset.x,
            Screen.height * 0.5f + screenCenterOffset.y
        );

        AimRay = aimCamera.ScreenPointToRay(screenPoint);

        if (Physics.Raycast(
            AimRay,
            out RaycastHit hit,
            aimDistance,
            aimLayers,
            triggerInteraction))
        {
            SetAimPoint(actor, hit.point);
            return;
        }

        Vector3 fallbackAimPoint = AimRay.origin + AimRay.direction * fallbackAimDistance;
        SetAimPoint(actor, fallbackAimPoint);
    }

    private void SetAimPoint(Transform actor, Vector3 aimPoint)
    {
        AimPointClamped = aimPoint;
        HasAimPoint = true;

        Vector3 facingDir;

        if (cameraTransform != null)
        {
            facingDir = cameraTransform.forward;
        }
        else
        {
            facingDir = aimPoint - actor.position;
        }

        facingDir.y = 0f;

        if (facingDir.sqrMagnitude > 0.0001f)
        {
            facingDir.Normalize();
            AimWorldDir = facingDir;
        }
    }

    private float NormalizeAngle(float angle)
    {
        while (angle > 180f)
            angle -= 360f;

        while (angle < -180f)
            angle += 360f;

        return angle;
    }
}