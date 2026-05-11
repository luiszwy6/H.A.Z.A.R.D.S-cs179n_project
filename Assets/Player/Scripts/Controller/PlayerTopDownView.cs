using UnityEngine;

[DisallowMultipleComponent]
public class PlayerTopDownView : PlayerMovementViewBase
{
    [Header("Refs")]
    [SerializeField] private PlayerAimSettings aimSettings;

    [Header("Movement Direction")]
    [SerializeField] private bool useCameraRelativeMovement = false;
    [SerializeField] private Transform movementCameraTransform;

    public override bool IsViewAiming => aimSettings != null && aimSettings.IsAiming;
    public override bool HasViewAimPoint => aimSettings != null && aimSettings.IsAiming;
    public override Vector3 ViewAimPoint => aimSettings != null ? aimSettings.AimPointClamped : transform.position;
    public override Vector3 ViewAimWorldDir => aimSettings != null ? aimSettings.AimWorldDir : transform.forward;

    private void Reset()
    {
        aimSettings = GetComponent<PlayerAimSettings>();

        if (aimSettings != null && aimSettings.cameraTransform != null)
            movementCameraTransform = aimSettings.cameraTransform;
    }

    private void Awake()
    {
        if (aimSettings == null)
            aimSettings = GetComponent<PlayerAimSettings>();
    }

    public override bool TryGetMuzzleAimPoint(out Vector3 aimPoint)
    {
        aimPoint = Vector3.zero;

        if (aimSettings == null)
            return false;

        if (!aimSettings.IsAiming)
            return false;

        aimPoint = aimSettings.AimPointClamped;
        return true;
    }

    public override PlayerMovementViewFrame BuildViewFrame(
        Transform actor,
        Vector2 moveInput,
        Vector3 fallbackMoveDirWorld,
        float inputMagnitude,
        bool wantsToMove,
        bool isCrouchingOrProne)
    {
        Vector3 moveDirWorld = useCameraRelativeMovement
            ? PlayerMovementViewUtility.GetCameraRelativeMoveDirection(moveInput, movementCameraTransform)
            : PlayerMovementViewUtility.GetWorldInputDirection(moveInput);

        if (!wantsToMove)
            moveDirWorld = Vector3.zero;

        Vector3 facingDir = moveDirWorld;
        bool rawAimHeld = false;
        bool aimPressedThisFrame = false;

        if (aimSettings != null)
        {
            facingDir = aimSettings.TickAimAndGetFacingDirection(
                actor,
                moveDirWorld,
                isCrouchingOrProne
            );

            rawAimHeld = aimSettings.IsAimHeld;
            aimPressedThisFrame = aimSettings.AimPressedThisFrame;
        }

        return new PlayerMovementViewFrame(
            moveDirWorld,
            facingDir,
            inputMagnitude,
            wantsToMove,
            rawAimHeld,
            aimPressedThisFrame
        );
    }

    public override void SetExternalAimOverride(bool active)
{
    if (aimSettings != null)
        aimSettings.SetExternalAimOverride(active);
}

public override void SetExternalAimOverride(bool active, float rotationSpeedOverride)
{
    if (aimSettings != null)
        aimSettings.SetExternalAimOverride(active, rotationSpeedOverride);
}

public override bool TryGetExternalAimRotationSpeedOverride(out float rotationSpeed)
{
    rotationSpeed = 0f;

    if (aimSettings == null)
        return false;

    return aimSettings.TryGetExternalAimRotationSpeedOverride(out rotationSpeed);
}

public override void CancelAimAndRequireRepress()
{
    if (aimSettings != null)
        aimSettings.CancelAimAndRequireRepress();
}
}