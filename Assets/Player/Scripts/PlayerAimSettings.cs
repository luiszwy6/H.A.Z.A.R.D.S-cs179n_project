using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class PlayerAimSettings : MonoBehaviour
{
    [Header("Aim Move Speeds")]
    public float aimWalkSpeed = 2.5f;
    public float aimCrouchSpeed = 1.5f;

    [Header("Camera")]
    public Transform cameraTransform;
    public Camera aimCamera;

    [Header("Mouse Aim")]
    public LayerMask mouseAimLayers = ~0;
    public float mouseAimDistance = 100f;

    public bool IsAiming { get; private set; }
    public Vector2 LookInput { get; private set; }
    public bool UsingMouseScheme { get; private set; }

    public Vector3 AimWorldDir { get; private set; }
    public Vector3 AimPointClamped { get; private set; }
    public bool HasMouseAimPoint { get; private set; }
    public Vector3 MouseAimPoint { get; private set; }

    private PlayerInput playerInput;
    private InputAction lookAction;
    private InputAction aimAction;

    void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
    }

    void OnEnable()
    {
        var actions = playerInput.actions;
        lookAction = actions["Look"];
        aimAction = actions["Aim"];

        lookAction?.Enable();
        aimAction?.Enable();
    }

    void OnDisable()
    {
        lookAction?.Disable();
        aimAction?.Disable();
    }

    public float GetMoveSpeed(bool isCrouching, float fallbackSpeed)
    {
        if (!IsAiming) return fallbackSpeed;
        return isCrouching ? aimCrouchSpeed : aimWalkSpeed;
    }

    public Vector3 TickAimAndGetFacingDirection(Transform actor, Vector3 moveDirWorld, bool isCrouching)
    {
        IsAiming = aimAction != null && aimAction.IsPressed();
        LookInput = lookAction != null ? lookAction.ReadValue<Vector2>() : Vector2.zero;

        UsingMouseScheme = playerInput != null &&
                           playerInput.currentControlScheme == "Keyboard&Mouse";

        Vector3 facingDir = moveDirWorld;

        AimWorldDir = Vector3.zero;
        AimPointClamped = actor.position;
        HasMouseAimPoint = false;
        MouseAimPoint = Vector3.zero;

        if (!IsAiming)
            return facingDir;

        if (UsingMouseScheme)
        {
            facingDir = GetMouseAimDirection(actor, moveDirWorld);
        }
        else
        {
            facingDir = GetStickAimDirection(actor, moveDirWorld);
        }

        if (facingDir.sqrMagnitude > 0.0001f)
        {
            facingDir.y = 0f;
            facingDir.Normalize();
            AimWorldDir = facingDir;
            AimPointClamped = actor.position + facingDir * 3f;
            return facingDir;
        }

        return moveDirWorld;
    }

    public Vector3 GetRollDirection(Vector3 moveDirWorld)
    {
        if (IsAiming && AimWorldDir.sqrMagnitude > 0.001f)
            return AimWorldDir;

        if (moveDirWorld.sqrMagnitude > 0.001f)
            return moveDirWorld;

        Vector3 f = transform.forward;
        f.y = 0f;
        return f.sqrMagnitude > 0.001f ? f.normalized : Vector3.forward;
    }

    Vector3 GetStickAimDirection(Transform actor, Vector3 moveDirWorld)
    {
        if (LookInput.sqrMagnitude < 0.0001f)
        {
            if (moveDirWorld.sqrMagnitude > 0.0001f)
                return moveDirWorld;

            Vector3 f = actor.forward;
            f.y = 0f;
            return f.normalized;
        }

        if (cameraTransform == null)
        {
            Vector3 dir = new Vector3(LookInput.x, 0f, LookInput.y);
            return dir.normalized;
        }

        Vector3 camForward = cameraTransform.forward;
        Vector3 camRight = cameraTransform.right;

        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        Vector3 dirWorld = camRight * LookInput.x + camForward * LookInput.y;
        dirWorld.y = 0f;

        return dirWorld.sqrMagnitude > 0.0001f ? dirWorld.normalized : actor.forward;
    }

    Vector3 GetMouseAimDirection(Transform actor, Vector3 moveDirWorld)
    {
        if (aimCamera == null)
        {
            if (cameraTransform != null)
            {
                Vector3 f = cameraTransform.forward;
                f.y = 0f;
                return f.sqrMagnitude > 0.0001f ? f.normalized : actor.forward;
            }

            return actor.forward;
        }

        Vector2 mousePos = Mouse.current != null
            ? Mouse.current.position.ReadValue()
            : Vector2.zero;

        Ray ray = aimCamera.ScreenPointToRay(mousePos);

        if (Physics.Raycast(ray, out RaycastHit hit, mouseAimDistance, mouseAimLayers, QueryTriggerInteraction.Ignore))
        {
            MouseAimPoint = hit.point;
            HasMouseAimPoint = true;
        }
        else
        {
            Plane groundPlane = new Plane(Vector3.up, actor.position);
            if (groundPlane.Raycast(ray, out float enter))
            {
                MouseAimPoint = ray.GetPoint(enter);
                HasMouseAimPoint = true;
            }
        }

        if (HasMouseAimPoint)
        {
            Vector3 dir = MouseAimPoint - actor.position;
            dir.y = 0f;

            if (dir.sqrMagnitude > 0.0001f)
            {
                dir.Normalize();
                AimPointClamped = MouseAimPoint;
                return dir;
            }
        }

        if (moveDirWorld.sqrMagnitude > 0.0001f)
            return moveDirWorld;

        Vector3 fallback = actor.forward;
        fallback.y = 0f;
        return fallback.normalized;
    }
}