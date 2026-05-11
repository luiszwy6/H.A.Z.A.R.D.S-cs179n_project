using UnityEngine;

public struct PlayerMovementViewFrame
{
    public Vector3 MoveDirWorld;
    public Vector3 FacingDir;
    public float InputMagnitude;
    public bool WantsToMove;
    public bool RawAimHeld;
    public bool AimPressedThisFrame;

    public PlayerMovementViewFrame(
        Vector3 moveDirWorld,
        Vector3 facingDir,
        float inputMagnitude,
        bool wantsToMove,
        bool rawAimHeld,
        bool aimPressedThisFrame)
    {
        MoveDirWorld = moveDirWorld;
        FacingDir = facingDir;
        InputMagnitude = inputMagnitude;
        WantsToMove = wantsToMove;
        RawAimHeld = rawAimHeld;
        AimPressedThisFrame = aimPressedThisFrame;
    }
}