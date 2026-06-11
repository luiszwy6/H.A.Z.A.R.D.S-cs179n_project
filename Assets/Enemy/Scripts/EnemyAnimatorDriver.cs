using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class EnemyAnimatorParameterDriver : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Animator animator;
    [SerializeField] private EnemyStatus enemyStatus;

    [Header("Move Speeds")]
    [SerializeField] private float walkSpeed = 2.0f;
    [SerializeField] private float runSpeed = 4.5f;
    [SerializeField] private float crouchSpeed = 1.4f;

    [Header("Min Agent Speeds")]
    [SerializeField] private bool useMinAgentSpeedByMoveMode = true;
    [SerializeField] private bool enforceMoveSpeedEveryFrame = true;
    [SerializeField] private float minWalkSpeed = 1.2f;
    [SerializeField] private float minRunSpeed = 3.5f;
    [SerializeField] private float minCrouchSpeed = 0.8f;

    [Header("Min Animation Speeds")]
    [SerializeField] private bool useMinAnimationSpeedWhileMoving = true;
    [SerializeField] private float minWalkAnimationSpeed = 1.0f;
    [SerializeField] private float minRunAnimationSpeed = 3.0f;
    [SerializeField] private float minCrouchAnimationSpeed = 0.7f;

    [Header("Current Move Mode")]
    [SerializeField] private EnemyMoveMode currentMoveMode = EnemyMoveMode.None;

    [Header("Movement Detection")]
    [SerializeField] private float movingThreshold = 0.1f;
    [SerializeField] private bool normalizeSpeed = true;
    [SerializeField] private float maxMoveSpeed = 4.5f;
    [SerializeField] private bool directionalBlendOnlyWhileAiming = true;

    [Header("Movement Intent")]
    [SerializeField] private bool useMoveIntentForAnimation = true;
    [SerializeField] private bool useDesiredVelocityWhenStarting = true;
    [SerializeField] private float moveIntentDistancePadding = 0.05f;

    [Header("Damping")]
    [SerializeField] private float dampTime = 0.12f;

    [Header("Combat State")]
    [SerializeField] private bool isAiming;
    [SerializeField] private bool isShooting;
    [SerializeField] private bool keepShooting;
    [SerializeField] private bool isReloading;

    [Header("External Animation Overrides")]
    [SerializeField] private bool externalCrouchOverride;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int SpeedXHash = Animator.StringToHash("SpeedX");
    private static readonly int SpeedZHash = Animator.StringToHash("SpeedZ");

    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");
    private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");
    private static readonly int IsIdleHash = Animator.StringToHash("IsIdle");

    private static readonly int IsAimingHash = Animator.StringToHash("IsAiming");
    private static readonly int IsShootingHash = Animator.StringToHash("IsShooting");
    private static readonly int KeepShootingHash = Animator.StringToHash("KeepShooting");
    private static readonly int IsReloadingHash = Animator.StringToHash("IsReloading");
    private static readonly int ShootHash = Animator.StringToHash("Shoot");

    private static readonly int QuickShotHash = Animator.StringToHash("QuickShot");

    private static readonly int IsCrouchingHash = Animator.StringToHash("IsCrouching");
    private static readonly int IsProneHash = Animator.StringToHash("IsProne");
    private static readonly int IsDivingHash = Animator.StringToHash("IsDiving");
    private static readonly int IsSlideHash = Animator.StringToHash("IsSlide");

    private static readonly int DiveDirXHash = Animator.StringToHash("DiveDirX");
    private static readonly int DiveDirYHash = Animator.StringToHash("DiveDirY");

    private void Awake()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (enemyStatus == null)
            enemyStatus = GetComponent<EnemyStatus>();

        ApplyMoveModeToAgent(currentMoveMode);
        ForceClearPlayerOnlyParams();
    }

    private void Update()
    {
        if (enforceMoveSpeedEveryFrame)
            ApplyMoveModeToAgent(currentMoveMode);

        UpdateMovementParams();
        UpdateCombatParams();
        ForceClearPlayerOnlyParams();
    }

    public void SetMoveMode(EnemyMoveMode mode)
    {
        currentMoveMode = mode;
        ApplyMoveModeToAgent(mode);
    }

    public EnemyMoveMode GetMoveMode()
    {
        return currentMoveMode;
    }

    public float GetMoveSpeed(EnemyMoveMode mode)
    {
        switch (mode)
        {
            case EnemyMoveMode.Run:
                return runSpeed;

            case EnemyMoveMode.Crouch:
                return crouchSpeed;

            case EnemyMoveMode.Walk:
                return walkSpeed;

            default:
                return 0f;
        }
    }

    private float GetMinAgentSpeed(EnemyMoveMode mode)
    {
        switch (mode)
        {
            case EnemyMoveMode.Run:
                return minRunSpeed;

            case EnemyMoveMode.Crouch:
                return minCrouchSpeed;

            case EnemyMoveMode.Walk:
                return minWalkSpeed;

            default:
                return 0f;
        }
    }

    private float GetMinAnimationSpeed(EnemyMoveMode mode)
    {
        switch (mode)
        {
            case EnemyMoveMode.Run:
                return minRunAnimationSpeed;

            case EnemyMoveMode.Crouch:
                return minCrouchAnimationSpeed;

            case EnemyMoveMode.Walk:
                return minWalkAnimationSpeed;

            default:
                return 0f;
        }
    }

    private void ApplyMoveModeToAgent(EnemyMoveMode mode)
    {
        if (agent == null)
            return;

        float speed = GetMoveSpeed(mode);

        if (useMinAgentSpeedByMoveMode)
            speed = Mathf.Max(speed, GetMinAgentSpeed(mode));

        if (speed > 0f)
            agent.speed = speed;
    }

    private void UpdateMovementParams()
    {
        if (agent == null || animator == null)
            return;

        if (!agent.enabled || !agent.isOnNavMesh)
            return;

        Vector3 velocity = agent.velocity;
        Vector3 desiredVelocity = agent.desiredVelocity;

        bool velocityMoving = velocity.magnitude > movingThreshold;

        bool hasMoveIntent =
            useMoveIntentForAnimation &&
            !agent.isStopped &&
            agent.hasPath &&
            !agent.pathPending &&
            agent.remainingDistance > agent.stoppingDistance + moveIntentDistancePadding;

        bool isMoving = velocityMoving || hasMoveIntent;

        Vector3 animationVelocity = velocity;

        if (useDesiredVelocityWhenStarting &&
            !velocityMoving &&
            hasMoveIntent &&
            desiredVelocity.sqrMagnitude > 0.01f)
        {
            animationVelocity = desiredVelocity;
        }

        if (useMinAnimationSpeedWhileMoving && isMoving)
        {
            float minAnimationSpeed = GetMinAnimationSpeed(currentMoveMode);

            if (minAnimationSpeed > 0f &&
                animationVelocity.sqrMagnitude > 0.0001f &&
                animationVelocity.magnitude < minAnimationSpeed)
            {
                animationVelocity = animationVelocity.normalized * minAnimationSpeed;
            }
        }

        Vector3 localVelocity = transform.InverseTransformDirection(animationVelocity);

        float speed = animationVelocity.magnitude;
        float speedX = localVelocity.x;
        float speedZ = localVelocity.z;

        if (normalizeSpeed)
        {
            float max = Mathf.Max(0.01f, maxMoveSpeed);
            speed = Mathf.Clamp01(speed / max);
            speedX = Mathf.Clamp(speedX / max, -1f, 1f);
            speedZ = Mathf.Clamp(speedZ / max, -1f, 1f);
        }

        bool isCrouching = externalCrouchOverride || currentMoveMode == EnemyMoveMode.Crouch;
        bool isRunning = isMoving && currentMoveMode == EnemyMoveMode.Run;
        bool isWalking = isMoving && currentMoveMode != EnemyMoveMode.Run;
        bool isIdle = !isMoving && !isAiming && !isShooting && !isReloading;

        animator.SetFloat(SpeedHash, speed, dampTime, Time.deltaTime);
        animator.SetFloat(SpeedXHash, speedX, dampTime, Time.deltaTime);
        animator.SetFloat(SpeedZHash, speedZ, dampTime, Time.deltaTime);

        animator.SetBool(IsMovingHash, isMoving);
        animator.SetBool(IsWalkingHash, isWalking);
        animator.SetBool(IsRunningHash, isRunning);
        animator.SetBool(IsCrouchingHash, isCrouching);
        animator.SetBool(IsIdleHash, isIdle);
    }

    private void UpdateCombatParams()
    {
        if (animator == null)
            return;

        animator.SetBool(IsAimingHash, isAiming);
        animator.SetBool(IsShootingHash, isShooting);
        animator.SetBool(KeepShootingHash, keepShooting);
        animator.SetBool(IsReloadingHash, isReloading);
    }

    private void ForceClearPlayerOnlyParams()
    {
        if (animator == null)
            return;

        animator.SetBool(QuickShotHash, false);
        animator.SetBool(IsProneHash, false);
        animator.SetBool(IsDivingHash, false);
        animator.SetBool(IsSlideHash, false);

        animator.SetFloat(DiveDirXHash, 0f);
        animator.SetFloat(DiveDirYHash, 0f);
    }

    public void SetAiming(bool aiming)
    {
        isAiming = aiming;
    }

    public void SetShooting(bool shooting)
    {
        isShooting = shooting;

        if (enemyStatus != null)
            enemyStatus.SetShooting(shooting || keepShooting);
    }

    public void SetKeepShooting(bool shooting)
    {
        keepShooting = shooting;

        if (enemyStatus != null)
            enemyStatus.SetShooting(isShooting || shooting);
    }

    public void TriggerShoot()
    {
        if (animator == null)
            return;

        animator.ResetTrigger(ShootHash);
        animator.SetTrigger(ShootHash);
    }

    public void SetReloading(bool reloading)
    {
        isReloading = reloading;

        if (enemyStatus != null)
            enemyStatus.SetReloading(reloading);
    }

    public void ClearCombat()
    {
        isAiming = false;
        isShooting = false;
        keepShooting = false;
        isReloading = false;

        if (enemyStatus != null)
        {
            enemyStatus.SetShooting(false);
            enemyStatus.SetReloading(false);
        }
    }

    public void SetExternalCrouchOverride(bool crouching)
    {
        externalCrouchOverride = crouching;
    }
}
