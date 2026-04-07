using UnityEngine;
using Unity.Cinemachine;

public class CameraNoiseByMovement : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Animator animator;
    [SerializeField] private CinemachineBasicMultiChannelPerlin noise;

    [Header("Noise Settings")]
    [SerializeField] private float walkAmplitude = 0.25f;
    [SerializeField] private float runAmplitude = 0.6f;
    [SerializeField] private float frequency = 2.0f;

    [Header("Noise Smoothing")]
    [SerializeField] private float noiseBlendSpeed = 5f;

    private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");
    private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");

    private void Reset()
    {
        animator = GetComponentInParent<Animator>();

        if (noise == null)
            noise = GetComponent<CinemachineBasicMultiChannelPerlin>();
    }

    private void Awake()
    {
        if (noise == null)
            noise = GetComponent<CinemachineBasicMultiChannelPerlin>();

        if (noise != null)
            noise.FrequencyGain = frequency;
    }

    private void Update()
    {
        if (animator == null || noise == null)
            return;

        bool isWalking = animator.GetBool(IsWalkingHash);
        bool isRunning = animator.GetBool(IsRunningHash);

        float targetAmplitude = 0f;

        if (isRunning)
            targetAmplitude = runAmplitude;
        else if (isWalking)
            targetAmplitude = walkAmplitude;

        noise.AmplitudeGain = Mathf.MoveTowards(
            noise.AmplitudeGain,
            targetAmplitude,
            noiseBlendSpeed * Time.deltaTime
        );

        noise.FrequencyGain = frequency;
    }
}