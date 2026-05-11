using UnityEngine;

public abstract class PlayerMovementViewBase : MonoBehaviour
{
    public virtual bool IsViewAiming => false;
    public virtual bool HasViewAimPoint => false;
    public virtual Vector3 ViewAimPoint => transform.position;
    public virtual Vector3 ViewAimWorldDir => transform.forward;

    public virtual void ResetView(Transform actor) { }

    public virtual void SetExternalAimOverride(bool active) { }

    public virtual void SetExternalAimOverride(bool active, float rotationSpeedOverride) { }

    public virtual void CancelAimRuntimeOnly() { }

    public virtual void CancelAimAndRequireRepress() { }

    public virtual bool TryGetExternalAimRotationSpeedOverride(out float rotationSpeed)
    {
        rotationSpeed = 0f;
        return false;
    }

    public virtual bool TryGetMuzzleAimPoint(out Vector3 aimPoint)
    {
        aimPoint = Vector3.zero;
        return false;
    }

    public virtual bool TryGetViewShotRay(out Ray shotRay)
    {
        shotRay = default;
        return false;
    }

    public abstract PlayerMovementViewFrame BuildViewFrame(
        Transform actor,
        Vector2 moveInput,
        Vector3 fallbackMoveDirWorld,
        float inputMagnitude,
        bool wantsToMove,
        bool isCrouchingOrProne
    );
}