using UnityEngine;
using Unity.Cinemachine;

public class TpsCameraDynamicFOV : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Animator animator;
    [SerializeField] private CinemachineCamera cinemachineCamera;

    [Header("FOV")]
    [SerializeField] private float defaultFOV = 60.0f;
    [SerializeField] private float quickShotFOV = 55.0f;
    [SerializeField] private float runningFOV = 68.0f;
    [SerializeField] private float crouchingFOV = 57.0f;
    [SerializeField] private float proneFOV = 54.0f;

    [Header("Smoothing")]
    [SerializeField] private float fovBlendSpeed = 20.0f;

    [Header("Priority")]
    [SerializeField] private bool quickShotHasHighestPriority = true;

    private static readonly int QuickShotHash = Animator.StringToHash("QuickShot");
    private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");
    private static readonly int IsCrouchingHash = Animator.StringToHash("IsCrouching");
    private static readonly int IsProneHash = Animator.StringToHash("IsProne");

    private void Reset()
    {
        animator = GetComponentInParent<Animator>();

        if (cinemachineCamera == null)
            cinemachineCamera = GetComponent<CinemachineCamera>();
    }

    private void Awake()
    {
        if (cinemachineCamera == null)
            cinemachineCamera = GetComponent<CinemachineCamera>();

        if (cinemachineCamera != null && defaultFOV <= 0f)
        {
            var lens = cinemachineCamera.Lens;
            defaultFOV = lens.FieldOfView;
        }
    }

    private void Update()
    {
        if (animator == null || cinemachineCamera == null)
            return;

        bool isQuickShot = animator.GetBool(QuickShotHash);
        bool isRunning = animator.GetBool(IsRunningHash);
        bool isCrouching = animator.GetBool(IsCrouchingHash);
        bool isProne = animator.GetBool(IsProneHash);

        float targetFOV = ResolveTargetFOV(
            isQuickShot,
            isRunning,
            isCrouching,
            isProne
        );

        var lens = cinemachineCamera.Lens;
        lens.FieldOfView = Mathf.MoveTowards(
            lens.FieldOfView,
            targetFOV,
            fovBlendSpeed * Time.deltaTime
        );
        cinemachineCamera.Lens = lens;
    }

    private float ResolveTargetFOV(
        bool isQuickShot,
        bool isRunning,
        bool isCrouching,
        bool isProne)
    {
        if (quickShotHasHighestPriority && isQuickShot)
            return quickShotFOV;

        if (isRunning)
            return runningFOV;

        if (isProne)
            return proneFOV;

        if (isCrouching)
            return crouchingFOV;

        return defaultFOV;
    }
}