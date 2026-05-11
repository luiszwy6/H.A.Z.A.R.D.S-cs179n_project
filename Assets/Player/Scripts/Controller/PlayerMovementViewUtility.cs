using UnityEngine;

public static class PlayerMovementViewUtility
{
    public static Vector3 GetWorldInputDirection(Vector2 moveInput)
    {
        Vector3 dir = new Vector3(moveInput.x, 0f, moveInput.y);

        if (dir.sqrMagnitude <= 0.0001f)
            return Vector3.zero;

        return dir.normalized;
    }

    public static Vector3 GetCameraRelativeMoveDirection(Vector2 moveInput, Transform referenceTransform)
    {
        if (moveInput.sqrMagnitude <= 0.0001f)
            return Vector3.zero;

        if (referenceTransform == null)
            return GetWorldInputDirection(moveInput);

        Vector3 camForward = referenceTransform.forward;
        Vector3 camRight = referenceTransform.right;

        camForward.y = 0f;
        camRight.y = 0f;

        if (camForward.sqrMagnitude > 0.0001f)
            camForward.Normalize();

        if (camRight.sqrMagnitude > 0.0001f)
            camRight.Normalize();

        Vector3 moveDir = camForward * moveInput.y + camRight * moveInput.x;
        moveDir.y = 0f;

        return moveDir.sqrMagnitude > 0.0001f
            ? moveDir.normalized
            : Vector3.zero;
    }

    public static float NormalizeAngle(float angle)
    {
        while (angle > 180f)
            angle -= 360f;

        while (angle < -180f)
            angle += 360f;

        return angle;
    }
}