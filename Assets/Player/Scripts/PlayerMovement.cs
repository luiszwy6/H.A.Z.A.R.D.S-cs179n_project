using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(PlayerAimSettings))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Parameters")]
    public float walkSpeed = 4f;
    public float runSpeed = 6f;
    public float crouchSpeed = 2f;
    public float proneSpeed = 1.2f;
    public float gravity = -9.81f;

    [Header("Rotation")]
    public float rotationSpeed = 10f;

    [Header("Camera Transform")]
    public Transform cameraTransform;

    [Header("Animation")]
    public float speedDampTime = 0.1f;

    [Header("Dive Settings")]
    public float diveDistance = 4f;
    public float diveDuration = 0.45f;
    public float diveCooldown = 0.8f;

    [Header("External Locks")]
    public bool externalMovementLock = false;

    private Animator animator;
    private CharacterController controller;
    private PlayerInput playerInput;
    private PlayerAimSettings aimSettings;

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

    // Hold-prone helpers
    private bool proneHoldInProgress;
    private bool suppressNextCrouchTap;

    // Aim/Run arbitration
    private bool preferAimWhenBothHeld = true;

    // Debug log
    private string lastLoggedState = "";
    private bool isWalkingState;
    private bool isRunningState;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();
        playerInput = GetComponent<PlayerInput>();
        aimSettings = GetComponent<PlayerAimSettings>();
    }

    void Start()
    {
        LogCurrentStateIfChanged();
    }

    void OnEnable()
    {
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
        float dt = Time.deltaTime;

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

        Vector2 moveInput = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        bool runHeld = runAction != null && runAction.IsPressed();

        if (externalMovementLock)
        {
            moveInput = Vector2.zero;
            runHeld = false;
        }

        if (crouchAction != null && crouchAction.WasPerformedThisFrame() && isGrounded && !isDiving)
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

        Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y);
        float inputMagnitude = Mathf.Clamp01(inputDir.magnitude);
        bool wantsToMove = inputMagnitude > 0.1f;

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
                           diveAction != null &&
                           diveAction.WasPerformedThisFrame();

        bool canDive = Time.time >= lastDiveTime + diveCooldown;

        if (divePressed && isGrounded && !isDiving && !isProne && canDive)
        {
            StartDive(moveDirWorld);
            UpdateDive(dt);
            LogCurrentStateIfChanged();
            return;
        }

        bool runRequested = !externalMovementLock &&
                            runHeld &&
                            wantsToMove &&
                            isGrounded &&
                            !proneHoldInProgress;

        bool runPressedThisFrame = !externalMovementLock &&
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

        if (aimPressedThisFrame)
            preferAimWhenBothHeld = true;

        if (runPressedThisFrame)
            preferAimWhenBothHeld = false;

        bool wantsToRun = false;
        isAiming = false;

        if (runRequested && rawAimHeld)
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

        float baseSpeed;
        if (isProne) baseSpeed = proneSpeed;
        else if (isCrouching) baseSpeed = crouchSpeed;
        else if (wantsToRun) baseSpeed = runSpeed;
        else baseSpeed = walkSpeed;

        float currentSpeed = baseSpeed;
        if (aimSettings != null)
        {
            currentSpeed = aimSettings.GetMoveSpeed(isCrouching, isProne, baseSpeed, isAiming);
        }

        Vector3 horizontal = moveDirWorld * currentSpeed * inputMagnitude;

        if (facingDir.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(facingDir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * dt);
        }

        velocity.y += gravity * dt;

        Vector3 motion = horizontal;
        motion.y += velocity.y;

        controller.Move(motion * dt);

        isRunningState = wantsToRun;
        isWalkingState = wantsToMove && !wantsToRun;

        if (animator != null)
        {
            bool isMoving = wantsToMove;
            bool isRunning = wantsToRun;
            bool isWalking = isMoving && !isRunning;
            bool isIdle = !isMoving && !isCrouching && !isProne && !isDiving && !isAiming;

            Vector3 localMove = Vector3.zero;
            if (horizontal.sqrMagnitude > 0.0001f)
                localMove = transform.InverseTransformDirection(horizontal).normalized * inputMagnitude;

            if (!isAiming)
            {
                animator.SetFloat("Speed", inputMagnitude, speedDampTime, dt);
                animator.SetFloat("SpeedX", 0f, speedDampTime, dt);
                animator.SetFloat("SpeedZ", 0f, speedDampTime, dt);
            }
            else
            {
                animator.SetFloat("Speed", 0f, speedDampTime, dt);
                animator.SetFloat("SpeedX", localMove.x, speedDampTime, dt);
                animator.SetFloat("SpeedZ", localMove.z, speedDampTime, dt);
            }

            animator.SetBool("IsMoving", isMoving);
            animator.SetBool("IsCrouching", isCrouching);
            animator.SetBool("IsProne", isProne);
            animator.SetBool("IsDiving", isDiving);
            animator.SetBool("IsRunning", isRunning);
            animator.SetBool("IsWalking", isWalking);
            animator.SetBool("IsAiming", isAiming);
            animator.SetBool("IsIdle", isIdle);
        }

        LogCurrentStateIfChanged();
    }

    void OnProneStarted(InputAction.CallbackContext ctx)
    {
        if (!isGrounded || isDiving)
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
        if (!isGrounded || isDiving)
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

    void StartDive(Vector3 moveDirWorld)
    {
        Vector3 dir = moveDirWorld;

        if (aimSettings != null)
            dir = aimSettings.GetRollDirection(moveDirWorld);

        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            dir = transform.forward;

        dir.Normalize();

        diveDirection = dir;
        diveRemainingDistance = diveDistance;
        diveTimer = diveDuration;
        isDiving = true;
        isProne = false;
        isCrouching = false;
        isAiming = false;
        lastDiveTime = Time.time;

        if (animator != null)
        {
            animator.ResetTrigger("DiveTrigger");
            animator.SetTrigger("DiveTrigger");

            animator.SetBool("IsDiving", true);
            animator.SetBool("IsProne", false);
            animator.SetBool("IsCrouching", false);
            animator.SetBool("IsRunning", false);
            animator.SetBool("IsWalking", false);
            animator.SetBool("IsMoving", false);
            animator.SetBool("IsAiming", false);
            animator.SetBool("IsIdle", false);

            Vector3 localDiveDir = transform.InverseTransformDirection(dir);
            animator.SetFloat("DiveDirX", localDiveDir.x);
            animator.SetFloat("DiveDirY", localDiveDir.z);
        }
    }

    void UpdateDive(float dt)
    {
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

            if (animator != null)
            {
                animator.SetBool("IsDiving", false);
                animator.SetBool("IsProne", false);
                animator.SetBool("IsCrouching", true);
                animator.SetBool("IsAiming", false);
            }
        }
    }

    string GetCurrentStateName()
    {
        if (isDiving) return "Diving";
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