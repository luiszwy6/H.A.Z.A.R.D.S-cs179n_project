using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerCoverController : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private string interactActionName = "Interact";
    [SerializeField] private string runActionName = "Run";

    [Header("Refs")]
    [SerializeField] private PlayerStatus playerStatus;
    [SerializeField] private PlayerMovement playerMovement;

    [Header("Cover Enter")]
    [SerializeField] private bool autoEnterCover = true;
    [SerializeField] private float playerRadius = 0.35f;
    [SerializeField] private float enterCoverDuration = 0.12f;

    [Tooltip("Prevents Run/Interact from instantly exiting cover on the same moment the player auto-enters cover.")]
    [SerializeField] private float enterExitInputBlockDuration = 0.18f;

    [Header("Cover Type Behavior")]
    [SerializeField] private bool highCoverRotatesToCover = true;
    [SerializeField] private bool lowCoverForcesCrouch = true;

    [Header("Cover Enter Slowdown")]
    [SerializeField] private bool slowDownWhenEnteringCover = true;
    [SerializeField] private float enterCoverSpeedMultiplier = 0.45f;
    [SerializeField] private float enterCoverSlowDuration = 0.35f;

    [Header("Cover Exit Slowdown")]
    [SerializeField] private bool slowDownWhenLeavingCover = true;
    [SerializeField] private float leaveCoverSpeedMultiplier = 0.35f;
    [SerializeField] private float leaveCoverSlowDuration = 0.45f;

    private CharacterController controller;
    private PlayerInput playerInput;

    private InputAction interactAction;
    private InputAction runAction;

    private readonly List<CoverTrigger> nearbyCovers = new List<CoverTrigger>();

    private CoverTrigger currentCover;
    private bool isInCover;
    private bool isMovingToCover;
    private bool suppressAutoEnterUntilExit;
    private bool lowCoverCrouchApplied;
    private Coroutine moveRoutine;

    private bool coverSlowActive;
    private float coverSlowEndTime;
    private float speedMultiplierBeforeCoverSlow = 1f;

    private float enteredCoverTime = -999f;

    public bool IsInCover => isInCover;
    public CoverTrigger CurrentCover => currentCover;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerInput = GetComponent<PlayerInput>();

        if (playerStatus == null)
            playerStatus = GetComponent<PlayerStatus>();

        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();
    }

    private void OnEnable()
    {
        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();

        if (playerInput == null || playerInput.actions == null)
            return;

        interactAction = playerInput.actions.FindAction(interactActionName, false);
        runAction = playerInput.actions.FindAction(runActionName, false);

        interactAction?.Enable();
        runAction?.Enable();

        if (interactAction != null)
            interactAction.performed += OnInteractPerformed;
        else
            Debug.LogWarning($"PlayerCoverController: Input action '{interactActionName}' was not found.", this);
    }

    private void OnDisable()
    {
        if (interactAction != null)
            interactAction.performed -= OnInteractPerformed;

        interactAction?.Disable();
        runAction?.Disable();

        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
            moveRoutine = null;
        }

        ReleaseLowCoverCrouch();

        if (currentCover != null)
            currentCover.Release(gameObject);

        currentCover = null;
        isInCover = false;
        isMovingToCover = false;
        suppressAutoEnterUntilExit = false;
        enteredCoverTime = -999f;

        if (playerStatus != null)
            playerStatus.SetInCover(false, null);

        ReleaseTemporaryEnterLock();
        RestoreCoverSlowdown();
    }

private void Update()
{
    UpdateCoverSlowdown();

    if (!isInCover && !isMovingToCover && autoEnterCover && !suppressAutoEnterUntilExit)
    {
        TryAutoEnterBestCover();
    }

    if (!isInCover || isMovingToCover)
        return;

    // Run no longer cancels cover.
    // Cover is only canceled by Interact or by leaving the current cover trigger.
    // PlayerMovement remains responsible for movement, turning, aiming, root motion, etc.
}

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (isMovingToCover)
            return;

        if (isInCover)
        {
            if (IsExitInputBlocked())
                return;

            ExitCover(true, false);
            return;
        }

        CoverTrigger bestCover = GetBestNearbyCover();
        if (bestCover != null)
            EnterCover(bestCover);
    }

    private void OnTriggerEnter(Collider other)
    {
        CoverTrigger cover = other.GetComponentInParent<CoverTrigger>();

        if (cover == null)
            return;

        RegisterNearbyCover(cover);

        if (autoEnterCover && !suppressAutoEnterUntilExit && !isInCover && !isMovingToCover)
        {
            EnterCover(cover);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        CoverTrigger cover = other.GetComponentInParent<CoverTrigger>();

        if (cover == null)
            return;

        RegisterNearbyCover(cover);

        if (autoEnterCover && !suppressAutoEnterUntilExit && !isInCover && !isMovingToCover)
        {
            EnterCover(cover);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        CoverTrigger cover = other.GetComponentInParent<CoverTrigger>();

        if (cover == null)
            return;

        nearbyCovers.Remove(cover);

        if (nearbyCovers.Count == 0)
            suppressAutoEnterUntilExit = false;

        if (cover == currentCover && isInCover)
        {
            ExitCover(false, true);
        }
    }

    private void RegisterNearbyCover(CoverTrigger cover)
    {
        if (cover == null)
            return;

        if (!nearbyCovers.Contains(cover))
            nearbyCovers.Add(cover);
    }

    private bool IsExitInputBlocked()
    {
        return Time.time < enteredCoverTime + Mathf.Max(0f, enterExitInputBlockDuration);
    }

    private void TryAutoEnterBestCover()
    {
        CoverTrigger bestCover = GetBestNearbyCover();

        if (bestCover == null)
            return;

        EnterCover(bestCover);
    }

    private CoverTrigger GetBestNearbyCover()
    {
        CoverTrigger bestCover = null;
        float bestDistanceSqr = float.MaxValue;

        for (int i = nearbyCovers.Count - 1; i >= 0; i--)
        {
            CoverTrigger cover = nearbyCovers[i];

            if (cover == null)
            {
                nearbyCovers.RemoveAt(i);
                continue;
            }

            if (!cover.IsAvailableFor(gameObject))
                continue;

            bool foundPose = cover.TryGetCoverPose(
                transform.position,
                playerRadius,
                out Vector3 coverPosition,
                out Quaternion coverRotation
            );

            if (!foundPose)
                continue;

            float distanceSqr = (coverPosition - transform.position).sqrMagnitude;

            if (distanceSqr < bestDistanceSqr)
            {
                bestDistanceSqr = distanceSqr;
                bestCover = cover;
            }
        }

        return bestCover;
    }

    private void EnterCover(CoverTrigger cover)
    {
        if (cover == null)
            return;

        if (currentCover == cover && (isInCover || isMovingToCover))
            return;

        if (!cover.Reserve(gameObject))
            return;

        bool foundPose = cover.TryGetCoverPose(
            transform.position,
            playerRadius,
            out Vector3 targetPosition,
            out Quaternion targetRotation
        );

        if (!foundPose)
        {
            cover.Release(gameObject);
            return;
        }

        if (currentCover != null && currentCover != cover)
            currentCover.Release(gameObject);

        ReleaseLowCoverCrouch();

        currentCover = cover;

        bool rotateToCover = cover.coverType == CoverType.High && highCoverRotatesToCover;
        bool forceCrouch = cover.coverType == CoverType.Low && lowCoverForcesCrouch;

        if (moveRoutine != null)
            StopCoroutine(moveRoutine);

        moveRoutine = StartCoroutine(
            MoveToCoverRoutine(
                targetPosition,
                targetRotation,
                rotateToCover,
                forceCrouch
            )
        );
    }

    private IEnumerator MoveToCoverRoutine(
        Vector3 targetPosition,
        Quaternion targetRotation,
        bool rotateToCover,
        bool forceCrouch)
    {
        isMovingToCover = true;
        isInCover = false;

        if (forceCrouch)
            ApplyLowCoverCrouch();

        if (rotateToCover)
        {
            Quaternion startRotation = transform.rotation;
            float elapsed = 0f;

            while (elapsed < enterCoverDuration)
            {
                elapsed += Time.deltaTime;

                float t = enterCoverDuration <= 0f
                    ? 1f
                    : Mathf.Clamp01(elapsed / enterCoverDuration);

                float smoothT = t * t * (3f - 2f * t);

                transform.rotation = Quaternion.Slerp(startRotation, targetRotation, smoothT);

                yield return null;
            }

            transform.rotation = targetRotation;
        }
        else
        {
            yield return null;
        }

        isMovingToCover = false;
        isInCover = true;
        enteredCoverTime = Time.time;

        if (playerStatus != null)
            playerStatus.SetInCover(true, currentCover);
    }

    private void ExitCover(bool suppressAutoReenter, bool applyLeaveSlowdown)
    {
        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
            moveRoutine = null;
        }

        ReleaseLowCoverCrouch();

        if (currentCover != null)
            currentCover.Release(gameObject);

        currentCover = null;
        isMovingToCover = false;
        isInCover = false;
        enteredCoverTime = -999f;

        if (suppressAutoReenter)
            suppressAutoEnterUntilExit = true;

        if (playerStatus != null)
            playerStatus.SetInCover(false, null);

        ReleaseTemporaryEnterLock();

        if (applyLeaveSlowdown && slowDownWhenLeavingCover)
        {
            ApplyCoverSlowdown(
                leaveCoverSpeedMultiplier,
                leaveCoverSlowDuration
            );
        }
    }

    private void ApplyLowCoverCrouch()
    {
        if (playerMovement == null)
            return;

        playerMovement.SetExternalCrouchLock(true, true);
        lowCoverCrouchApplied = true;
    }

    private void ReleaseLowCoverCrouch()
    {
        if (!lowCoverCrouchApplied)
            return;

        if (playerMovement != null)
            playerMovement.SetExternalCrouchLock(false, false);

        lowCoverCrouchApplied = false;
    }

    private void ApplyCoverSlowdown(float speedMultiplier, float duration)
    {
        if (playerMovement == null)
            return;

        if (duration <= 0f)
            return;

        if (!coverSlowActive)
        {
            speedMultiplierBeforeCoverSlow = playerMovement.externalSpeedMultiplier;
            coverSlowActive = true;
        }

        coverSlowEndTime = Time.time + Mathf.Max(0f, duration);

        float slowMultiplier = Mathf.Clamp(speedMultiplier, 0.01f, 1f);

        playerMovement.externalSpeedMultiplier = Mathf.Min(
            playerMovement.externalSpeedMultiplier,
            slowMultiplier
        );
    }

    private void UpdateCoverSlowdown()
    {
        if (!coverSlowActive)
            return;

        if (playerMovement == null)
        {
            coverSlowActive = false;
            return;
        }

        if (Time.time < coverSlowEndTime)
            return;

        RestoreCoverSlowdown();
    }

    private void RestoreCoverSlowdown()
    {
        if (!coverSlowActive)
            return;

        if (playerMovement != null)
            playerMovement.externalSpeedMultiplier = speedMultiplierBeforeCoverSlow;

        coverSlowActive = false;
    }

    private void ApplyTemporaryEnterLock()
    {
        if (playerMovement == null)
            return;

        playerMovement.externalMovementLock = true;
        playerMovement.externalAnimatorLocomotionLock = false;
    }

    private void ReleaseTemporaryEnterLock()
    {
        if (playerMovement == null)
            return;

        playerMovement.externalMovementLock = false;
        playerMovement.externalAnimatorLocomotionLock = false;
    }
}