using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(PlayerAimSettings))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Legacy Speeds (Animator/Input Reference Only)")]
    public float walkSpeed = 4f;
    public float runSpeed = 6f;
    public float crouchSpeed = 2f;
    public float proneSpeed = 1.2f;
    public float gravity = -9.81f;

    [Header("Rotation")]
    public float rotationSpeed = 10f;

    [Header("Root Motion")]
    public bool useRootMotionLocomotion = true;
    public bool useRootMotionRotation = false;
    public float rootMotionScale = 1f;

    [Header("External Speed Multipliers")]
    [Min(0.01f)] public float externalSpeedMultiplier = 1f;
    [Header("Camera Transform")]
    public Transform cameraTransform;

    [Header("Animation")]
    public float speedDampTime = 0.1f;
    public float riseSpeed = 2f;
    public float fallSpeed = 4f;

    
    [Header("Dive Settings")]
    public float diveDistance = 4f;
    public float diveDuration = 0.45f;
    public float diveCooldown = 0.8f;
    public float diveRecoveryLockDuration = 0.25f;
    public bool allowAimCancelDive = true;

    [Header("Slide Settings")]
    public float crouchSlideDistance = 4f;
    public float crouchSlideDuration = 0.45f;
    public float proneSlideDistance = 5.5f;
    public float proneSlideDuration = 0.65f;
    public float slideCooldown = 0.8f;
    public float slideRecoveryLockDuration = 0.25f;
    public bool allowAimCancelSlide = true;
    public float slideRotationSpeed = 14f;

    [Header("External Locks")]
    public bool externalMovementLock = false;

    private Animator animator;
    private CharacterController controller;
    private PlayerInput playerInput;
    private PlayerAimSettings aimSettings;

    private PlayerWeaponSlots playerWeaponSlots;
    private WeaponAmmoSettings fallbackWeaponAmmoSettings;

    private InputAction moveAction;
    private InputAction runAction;
    private InputAction crouchAction;
    private InputAction diveAction;
    private InputAction proneAction;

    private Vector3 velocity;
    private bool isGrounded;
    private bool isCrouching;
    private bool isProne;
    private bool isAiming;

    private bool isDiving;
    private float diveTimer;
    private float diveRemainingDistance;
    private Vector3 diveDirection;
    private float lastDiveTime = -999f;
    private float postDiveLockUntilTime = -999f;

    private bool isSliding;
    private float slideTimer;
    private Vector3 slideDirection;
    private float currentSlideDistance;
    private float currentSlideDuration;
    private float lastSlideTime = -999f;
    private float postSlideLockUntilTime = -999f;

    private bool proneHoldInProgress;
    private bool suppressNextCrouchTap;

    private bool preferAimWhenBothHeld = true;

    private bool hasMoveInput;
    private Vector3 desiredFacingDir = Vector3.forward;

    private float animSpeed;
    private float animSpeedX;
    private float animSpeedZ;

    private string lastLoggedState = "";
    private bool isWalkingState;
    private bool isRunningState;

    private static readonly int SlideTriggerHash = Animator.StringToHash("SlideTrigger");
    private static readonly int IsSlideHash = Animator.StringToHash("IsSlide");

    public bool IsRunningNow => isRunningState;
    public bool IsDivingNow => isDiving;
    public bool IsSlidingNow => isSliding;

    public bool IsRecoveryLockedNow =>
        Time.time < postDiveLockUntilTime || Time.time < postSlideLockUntilTime;

    public bool CanShootNow =>
        !isDiving &&
        !isSliding &&
        !isRunningState;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();
        playerInput = GetComponent<PlayerInput>();
        aimSettings = GetComponent<PlayerAimSettings>();
        playerWeaponSlots = GetComponent<PlayerWeaponSlots>();

        if (fallbackWeaponAmmoSettings == null)
            fallbackWeaponAmmoSettings = GetComponentInChildren<WeaponAmmoSettings>(true);
    }

    void Start()
    {
        LogCurrentStateIfChanged();
    }

    void OnEnable()
    {
        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();

        if (playerInput == null || playerInput.actions == null)
            return;

        var actions = playerInput.actions;

        moveAction = actions["Move"];
        runAction = actions["Run"];
        crouchAction = actions["Crouch"];
        diveAction = actions["Dive"];
        proneAction = actions["Prone"];

        moveAction?.Enable();
        runAction?.Enable();
        crouchAction?.Enable();
        diveAction?.Enable();
        proneAction?.Enable();

        if (proneAction != null)
        {
            proneAction.started += OnProneStarted;
            proneAction.performed += OnPronePerformed;
            proneAction.canceled += OnProneCanceled;
        }
    }

    void OnDisable()
    {
        if (proneAction != null)
        {
            proneAction.started -= OnProneStarted;
            proneAction.performed -= OnPronePerformed;
            proneAction.canceled -= OnProneCanceled;
        }

        moveAction?.Disable();
        runAction?.Disable();
        crouchAction?.Disable();
        diveAction?.Disable();
        proneAction?.Disable();
    }

void Update()
    {
        float speedMult = Mathf.Max(0.01f, externalSpeedMultiplier);
        float dt = Time.deltaTime * speedMult;

        isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0f)
            velocity.y = -0.5f;

        if (isDiving)
        {
            isWalkingState = false;
            isRunningState = false;
            UpdateDive(dt);
            LogCurrentStateIfChanged();
            return;
        }

        if (isSliding)
        {
            isWalkingState = false;
            isRunningState = false;
            UpdateSlide(dt);
            LogCurrentStateIfChanged();
            return;
        }

        bool postDiveLocked = Time.time < postDiveLockUntilTime;
        bool postSlideLocked = Time.time < postSlideLockUntilTime;
        bool recoveryLocked = postDiveLocked || postSlideLocked;

        Vector2 moveInput = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        bool runHeld = runAction != null && runAction.IsPressed();

        WeaponAmmoSettings currentAmmoSettings = GetCurrentAmmoSettings();
        bool isReloadingNow = currentAmmoSettings != null && currentAmmoSettings.IsReloading;

        if (externalMovementLock || recoveryLocked)
        {
            moveInput = Vector2.zero;
            runHeld = false;
        }

        if (isReloadingNow)
            runHeld = false;

        Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y);
        float inputMagnitude = Mathf.Clamp01(inputDir.magnitude);
        bool wantsToMove = inputMagnitude > 0.1f;
        hasMoveInput = wantsToMove;

        if (wantsToMove)
            inputDir.Normalize();
        else
            inputDir = Vector3.zero;

        Vector3 moveDirWorld = inputDir;
        if (cameraTransform != null && wantsToMove)
        {
            Vector3 camForward = cameraTransform.forward;
            Vector3 camRight = cameraTransform.right;

            camForward.y = 0f;
            camRight.y = 0f;

            camForward.Normalize();
            camRight.Normalize();

            moveDirWorld = camForward * moveInput.y + camRight * moveInput.x;
            moveDirWorld.y = 0f;
            moveDirWorld.Normalize();
        }

        bool divePressed = !externalMovementLock &&
                           !recoveryLocked &&
                           diveAction != null &&
                           diveAction.WasPerformedThisFrame();

        bool canDive = Time.time >= lastDiveTime + diveCooldown;

        if (divePressed && isGrounded && !isDiving && !isSliding && !isProne && canDive)
        {
            StartDive(moveDirWorld);
            UpdateDive(dt);
            LogCurrentStateIfChanged();
            return;
        }

        bool runRequested = !externalMovementLock &&
                            !recoveryLocked &&
                            runHeld &&
                            wantsToMove &&
                            isGrounded &&
                            !proneHoldInProgress;

        bool runPressedThisFrame = !externalMovementLock &&
                                   !recoveryLocked &&
                                   runAction != null &&
                                   runAction.WasPressedThisFrame();

        bool rawAimHeld = false;
        bool aimPressedThisFrame = false;

        Vector3 facingDir = moveDirWorld;

        if (aimSettings != null)
        {
            facingDir = aimSettings.TickAimAndGetFacingDirection(
                transform,
                moveDirWorld,
                isCrouching || isProne
            );

            rawAimHeld = aimSettings.IsAimHeld;
            aimPressedThisFrame = aimSettings.AimPressedThisFrame;
        }

        bool canSlideNow = !externalMovementLock &&
                           !recoveryLocked &&
                           isGrounded &&
                           !isDiving &&
                           !isSliding &&
                           !isProne &&
                           !proneHoldInProgress &&
                           runRequested &&
                           !rawAimHeld &&
                           Time.time >= lastSlideTime + slideCooldown;

        if (canSlideNow)
        {
            if (proneAction != null && proneAction.WasPerformedThisFrame())
            {
                StartSlide(moveDirWorld, true);
                UpdateSlide(dt);
                LogCurrentStateIfChanged();
                return;
            }

            if (crouchAction != null && crouchAction.WasPerformedThisFrame())
            {
                StartSlide(moveDirWorld, false);
                UpdateSlide(dt);
                LogCurrentStateIfChanged();
                return;
            }
        }

        if (crouchAction != null &&
            crouchAction.WasPerformedThisFrame() &&
            isGrounded &&
            !isDiving &&
            !isSliding &&
            !recoveryLocked)
        {
            if (suppressNextCrouchTap)
            {
                suppressNextCrouchTap = false;
            }
            else if (isProne)
            {
                isProne = false;
                isCrouching = true;
            }
            else
            {
                isCrouching = !isCrouching;
            }
        }

        if (aimPressedThisFrame)
            preferAimWhenBothHeld = true;
        else if (runPressedThisFrame)
            preferAimWhenBothHeld = false;

        bool wantsToRun = false;
        isAiming = false;

        if (aimPressedThisFrame && rawAimHeld)
        {
            isAiming = true;
            wantsToRun = false;
        }
        else if (runRequested && rawAimHeld)
        {
            if (preferAimWhenBothHeld)
            {
                isAiming = true;
                wantsToRun = false;
            }
            else
            {
                isAiming = false;
                wantsToRun = true;
            }
        }
        else if (rawAimHeld)
        {
            isAiming = true;
            wantsToRun = false;
        }
        else if (runRequested)
        {
            isAiming = false;
            wantsToRun = true;
        }
        else
        {
            isAiming = false;
            wantsToRun = false;
        }

        if (wantsToRun)
        {
            isCrouching = false;
            isProne = false;

            if (moveDirWorld.sqrMagnitude > 0.0001f)
                facingDir = moveDirWorld;
        }

        desiredFacingDir = facingDir;

        if (!useRootMotionRotation && desiredFacingDir.sqrMagnitude > 0.0001f)
        {
            float activeRotationSpeed = rotationSpeed;

            if (aimSettings != null &&
                aimSettings.TryGetExternalAimRotationSpeedOverride(out float overrideRotationSpeed))
            {
                activeRotationSpeed = overrideRotationSpeed;
            }

            Quaternion targetRot = Quaternion.LookRotation(desiredFacingDir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, activeRotationSpeed * dt);
        }

        velocity.y += gravity * dt;

        isRunningState = wantsToRun;
        isWalkingState = wantsToMove && !wantsToRun;

        if (animator != null)
        {
            bool isMoving = wantsToMove;
            bool isRunning = wantsToRun;
            bool isWalking = isMoving && !isRunning;
            bool isIdle = !isMoving && !isCrouching && !isProne && !isDiving && !isSliding && !isAiming;

            Vector3 localMove = Vector3.zero;
            if (moveDirWorld.sqrMagnitude > 0.0001f)
                localMove = transform.InverseTransformDirection(moveDirWorld).normalized * inputMagnitude;

            float targetSpeed = inputMagnitude;
            float targetSpeedX = 0f;
            float targetSpeedZ = 0f;

            if (isAiming)
            {
                targetSpeedX = localMove.x;
                targetSpeedZ = localMove.z;
            }

            UpdateLocomotionParams(targetSpeed, targetSpeedX, targetSpeedZ, dt);

            animator.SetFloat("Speed", animSpeed);
            animator.SetFloat("SpeedX", animSpeedX);
            animator.SetFloat("SpeedZ", animSpeedZ);

            animator.SetBool("IsMoving", isMoving);
            animator.SetBool("IsCrouching", isCrouching);
            animator.SetBool("IsProne", isProne);
            animator.SetBool("IsDiving", isDiving);
            animator.SetBool("IsSlide", isSliding);
            animator.SetBool("IsRunning", isRunning);
            animator.SetBool("IsWalking", isWalking);
            animator.SetBool("IsAiming", isAiming);
            animator.SetBool("IsIdle", isIdle);
        }

        LogCurrentStateIfChanged();
    }

void OnAnimatorMove()
    {
        if (animator == null || controller == null)
            return;

        bool postDiveLocked = Time.time < postDiveLockUntilTime;
        bool postSlideLocked = Time.time < postSlideLockUntilTime;
        bool recoveryLocked = postDiveLocked || postSlideLocked;

        if (isDiving)
            return;

        if (isSliding)
            return;

        Vector3 delta = Vector3.zero;

        if (useRootMotionLocomotion && hasMoveInput && !externalMovementLock && !recoveryLocked)
        {
            float speedMult = Mathf.Max(0.01f, externalSpeedMultiplier);
            delta = animator.deltaPosition * rootMotionScale * speedMult;
            delta.y = 0f;
        }

        float gravityMult = Mathf.Max(0.01f, externalSpeedMultiplier);
        delta.y += velocity.y * Time.deltaTime * gravityMult;

        CollisionFlags flags = controller.Move(delta);

        if ((flags & CollisionFlags.Below) != 0 && velocity.y < 0f)
            velocity.y = -0.5f;

        if (useRootMotionRotation)
            transform.rotation *= animator.deltaRotation;
    }

    void OnProneStarted(InputAction.CallbackContext ctx)
    {
        if (!isGrounded || isDiving || isSliding)
            return;

        if (CanStartSlideFromCurrentInput())
            return;

        proneHoldInProgress = true;

        if (isProne)
            return;

        if (!isCrouching)
        {
            isCrouching = true;
            suppressNextCrouchTap = true;
        }
    }

    void OnPronePerformed(InputAction.CallbackContext ctx)
    {
        if (!isGrounded || isDiving || isSliding)
            return;

        if (CanStartSlideFromCurrentInput())
            return;

        proneHoldInProgress = false;
        suppressNextCrouchTap = false;

        if (isProne)
        {
            isProne = false;
            isCrouching = false;
            return;
        }

        isProne = true;
        isCrouching = false;
    }

    void OnProneCanceled(InputAction.CallbackContext ctx)
    {
        proneHoldInProgress = false;
    }

    WeaponAmmoSettings GetCurrentAmmoSettings()
    {
        if (playerWeaponSlots != null && playerWeaponSlots.CurrentAmmoSettings != null)
            return playerWeaponSlots.CurrentAmmoSettings;

        return fallbackWeaponAmmoSettings;
    }

    bool CanStartSlideFromCurrentInput()
    {
        if (moveAction == null || runAction == null)
            return false;

        bool postDiveLocked = Time.time < postDiveLockUntilTime;
        bool postSlideLocked = Time.time < postSlideLockUntilTime;
        bool recoveryLocked = postDiveLocked || postSlideLocked;

        if (externalMovementLock || recoveryLocked)
            return false;

        if (!controller.isGrounded)
            return false;

        if (isDiving || isSliding || isProne || proneHoldInProgress)
            return false;

        if (Time.time < lastSlideTime + slideCooldown)
            return false;

        Vector2 moveInput = moveAction.ReadValue<Vector2>();
        bool wantsToMove = moveInput.sqrMagnitude > 0.01f;
        bool runHeld = runAction.IsPressed();

        return runHeld && wantsToMove;
    }

    void StartDive(Vector3 moveDirWorld)
    {
        Vector3 dir = moveDirWorld.sqrMagnitude > 0.0001f ? moveDirWorld : transform.forward;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector3.forward;

        dir.Normalize();

        diveDirection = dir;
        diveRemainingDistance = diveDistance;
        diveTimer = diveDuration;
        isDiving = true;
        isSliding = false;
        isProne = false;
        isCrouching = false;
        isAiming = false;
        lastDiveTime = Time.time;

        if (animator != null)
        {
            animator.ResetTrigger("DiveTrigger");
            animator.SetTrigger("DiveTrigger");

            animator.SetBool("IsDiving", true);
            animator.SetBool("IsSlide", false);
            animator.SetBool("IsProne", false);
            animator.SetBool("IsCrouching", false);
            animator.SetBool("IsRunning", false);
            animator.SetBool("IsWalking", false);
            animator.SetBool("IsMoving", false);
            animator.SetBool("IsAiming", false);
            animator.SetBool("IsIdle", false);

            ResetLocomotionParams();

            Vector3 localDiveDir = transform.InverseTransformDirection(dir);
            animator.SetFloat("DiveDirX", localDiveDir.x);
            animator.SetFloat("DiveDirY", localDiveDir.z);
        }
    }

void UpdateDive(float dt)
    {
        float speedMult = Mathf.Max(0.01f, externalSpeedMultiplier);
        dt *= speedMult;

        // Dive can only be canceled by a new Aim press during Dive
        if (allowAimCancelDive && aimSettings != null && aimSettings.AimPressedThisFrame)
        {
            CancelDiveIntoAim();
            return;
        }

        float diveSpeed = diveDistance / Mathf.Max(diveDuration, 0.0001f);

        float step = diveSpeed * dt;
        if (step > diveRemainingDistance)
            step = diveRemainingDistance;

        Vector3 motion = diveDirection * step;
        diveRemainingDistance -= step;
        controller.Move(motion);

        velocity.y += gravity * dt;
        controller.Move(new Vector3(0f, velocity.y, 0f) * dt);

        if (diveDirection.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(diveDirection, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * dt);
        }

        diveTimer -= dt;

        if (diveTimer <= 0f || diveRemainingDistance <= 0f)
        {
            isDiving = false;
            isProne = false;
            isCrouching = true;
            isAiming = false;
            postDiveLockUntilTime = Time.time + diveRecoveryLockDuration;

            if (animator != null)
            {
                animator.SetBool("IsDiving", false);
                animator.SetBool("IsProne", false);
                animator.SetBool("IsCrouching", true);
                animator.SetBool("IsAiming", false);
            }
        }
    }

    void CancelDiveIntoAim()
    {
        isDiving = false;
        isProne = false;
        isCrouching = false;
        isAiming = true;

        diveTimer = 0f;
        diveRemainingDistance = 0f;
        diveDirection = Vector3.zero;

        postDiveLockUntilTime = -999f;
        externalMovementLock = false;

        hasMoveInput = false;
        isWalkingState = false;
        isRunningState = false;

        Vector3 aimFacingDir = transform.forward;

        if (aimSettings != null)
        {
            aimFacingDir = aimSettings.TickAimAndGetFacingDirection(
                transform,
                Vector3.zero,
                false
            );

            if (aimFacingDir.sqrMagnitude < 0.0001f)
                aimFacingDir = transform.forward;
        }

        desiredFacingDir = aimFacingDir;

        if (!useRootMotionRotation && desiredFacingDir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(desiredFacingDir, Vector3.up);

        if (animator != null)
        {
            animator.SetBool("IsDiving", false);
            animator.SetBool("IsProne", false);
            animator.SetBool("IsCrouching", false);
            animator.SetBool("IsRunning", false);
            animator.SetBool("IsWalking", false);
            animator.SetBool("IsMoving", false);
            animator.SetBool("IsAiming", true);
            animator.SetBool("IsIdle", false);

            ResetLocomotionParams();
        }
    }

    void StartSlide(Vector3 moveDirWorld, bool useProneSlide)
    {
        isSliding = true;
        isDiving = false;
        isProne = false;
        isCrouching = false;
        isAiming = false;

        lastSlideTime = Time.time;
        currentSlideDistance = useProneSlide ? proneSlideDistance : crouchSlideDistance;
        currentSlideDuration = useProneSlide ? proneSlideDuration : crouchSlideDuration;
        slideTimer = currentSlideDuration;

        slideDirection = moveDirWorld.sqrMagnitude > 0.0001f ? moveDirWorld : transform.forward;
        slideDirection.y = 0f;

        if (slideDirection.sqrMagnitude < 0.0001f)
            slideDirection = transform.forward;

        slideDirection.Normalize();

        externalMovementLock = true;

        if (animator != null)
        {
            animator.ResetTrigger(SlideTriggerHash);
            animator.SetTrigger(SlideTriggerHash);

            animator.SetBool(IsSlideHash, true);
            animator.SetBool("IsDiving", false);
            animator.SetBool("IsProne", false);
            animator.SetBool("IsCrouching", false);
            animator.SetBool("IsRunning", false);
            animator.SetBool("IsWalking", false);
            animator.SetBool("IsMoving", false);
            animator.SetBool("IsAiming", false);
            animator.SetBool("IsIdle", false);

            ResetLocomotionParams();
        }
    }

void UpdateSlide(float dt)
    {
        float speedMult = Mathf.Max(0.01f, externalSpeedMultiplier);
        dt *= speedMult;

        // Slide can only be canceled by a new Aim press during Slide
        if (allowAimCancelSlide && aimSettings != null && aimSettings.AimPressedThisFrame)
        {
            CancelSlideIntoAim();
            return;
        }

        float slideSpeed = currentSlideDistance / Mathf.Max(currentSlideDuration, 0.0001f);
        Vector3 horizontal = slideDirection * slideSpeed;

        velocity.y += gravity * dt;

        Vector3 motion = horizontal;
        motion.y = velocity.y;

        controller.Move(motion * dt);

        if (slideDirection.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(slideDirection, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, slideRotationSpeed * dt);
        }

        slideTimer -= dt;

        if (slideTimer <= 0f)
            EndSlide(applyRecoveryLock: true);
    }

    void CancelSlideIntoAim()
    {
        isSliding = false;
        isProne = false;
        isCrouching = false;
        isAiming = true;

        slideTimer = 0f;
        slideDirection = Vector3.zero;
        postSlideLockUntilTime = -999f;
        externalMovementLock = false;

        hasMoveInput = false;
        isWalkingState = false;
        isRunningState = false;

        Vector3 aimFacingDir = transform.forward;

        if (aimSettings != null)
        {
            aimFacingDir = aimSettings.TickAimAndGetFacingDirection(
                transform,
                Vector3.zero,
                false
            );

            if (aimFacingDir.sqrMagnitude < 0.0001f)
                aimFacingDir = transform.forward;
        }

        desiredFacingDir = aimFacingDir;

        if (!useRootMotionRotation && desiredFacingDir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(desiredFacingDir, Vector3.up);

        if (animator != null)
        {
            animator.SetBool(IsSlideHash, false);
            animator.SetBool("IsAiming", true);
            animator.SetBool("IsIdle", false);
            animator.SetBool("IsMoving", false);
            animator.SetBool("IsWalking", false);
            animator.SetBool("IsRunning", false);

            ResetLocomotionParams();
        }
    }

    void EndSlide(bool applyRecoveryLock)
    {
        isSliding = false;
        isProne = false;
        isCrouching = true;
        isAiming = false;
        externalMovementLock = false;

        if (animator != null)
        {
            animator.SetBool(IsSlideHash, false);
            animator.SetBool("IsCrouching", true);
            animator.SetBool("IsAiming", false);
        }

        if (applyRecoveryLock)
            postSlideLockUntilTime = Time.time + slideRecoveryLockDuration;
        else
            postSlideLockUntilTime = -999f;
    }

    void UpdateLocomotionParams(float targetSpeed, float targetSpeedX, float targetSpeedZ, float dt)
    {
        float rise = riseSpeed * dt;
        float fall = fallSpeed * dt;

        animSpeed = Mathf.MoveTowards(
            animSpeed,
            targetSpeed,
            targetSpeed > animSpeed ? rise : fall
        );

        animSpeedX = Mathf.MoveTowards(
            animSpeedX,
            targetSpeedX,
            Mathf.Abs(targetSpeedX) > Mathf.Abs(animSpeedX) ? rise : fall
        );

        animSpeedZ = Mathf.MoveTowards(
            animSpeedZ,
            targetSpeedZ,
            Mathf.Abs(targetSpeedZ) > Mathf.Abs(animSpeedZ) ? rise : fall
        );
    }

    void ResetLocomotionParams()
    {
        animSpeed = 0f;
        animSpeedX = 0f;
        animSpeedZ = 0f;

        if (animator != null)
        {
            animator.SetFloat("Speed", 0f);
            animator.SetFloat("SpeedX", 0f);
            animator.SetFloat("SpeedZ", 0f);
        }
    }

    string GetCurrentStateName()
    {
        if (isDiving) return "Diving";
        if (isSliding) return "Sliding";
        if (isRunningState) return "Running";
        if (isWalkingState) return "Walking";
        if (isProne) return "Prone";
        if (isCrouching) return "Crouching";
        if (isAiming) return "Aiming";
        return "Idle";
    }

    void LogCurrentStateIfChanged()
    {
        string currentState = GetCurrentStateName();

        if (currentState != lastLoggedState)
        {
            lastLoggedState = currentState;
            Debug.Log($"[PlayerMovement] State -> {currentState}", this);
        }
    }
}