using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class PlayerSoundEffects : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerStatus playerStatus;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private CharacterController characterController;

    [Header("Hit Sounds")]
    [SerializeField] private AudioClip[] hitSounds;
    [Range(0f, 1f)] [SerializeField] private float hitVolume = 1f;

    [Header("Footstep — Walk")]
    [SerializeField] private AudioClip[] walkFootsteps;
    [Range(0f, 1f)] [SerializeField] private float walkFootstepVolume = 0.6f;
    [Min(0.1f)] [SerializeField] private float walkStepDistance = 1.8f;

    [Header("Footstep — Crouch Walk")]
    [SerializeField] private AudioClip[] crouchFootsteps;
    [Range(0f, 1f)] [SerializeField] private float crouchFootstepVolume = 0.35f;
    [Min(0.1f)] [SerializeField] private float crouchStepDistance = 1.4f;

    [Header("Footstep — Run")]
    [SerializeField] private AudioClip[] runFootsteps;
    [Range(0f, 1f)] [SerializeField] private float runFootstepVolume = 0.8f;
    [Min(0.1f)] [SerializeField] private float runStepDistance = 2.2f;

    [Header("Reload")]
    [SerializeField] private AudioClip reloadSound;
    [Range(0f, 1f)] [SerializeField] private float reloadVolume = 0.9f;

    [Header("Footstep Settings")]
    [SerializeField] private float minMoveSpeedForFootstep = 0.3f;

    private AudioSource audioSource;
    private Vector3 lastFootstepPosition;
    private float distanceTraveled;
    private int lastFootstepIndex = -1;
    private int lastHitIndex = -1;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        if (playerStatus == null)
            playerStatus = GetComponent<PlayerStatus>();

        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();

        if (characterController == null)
            characterController = GetComponent<CharacterController>();

        lastFootstepPosition = transform.position;
    }

    private void OnEnable()
    {
        if (playerHealth != null)
            playerHealth.onDamaged.AddListener(OnHit);
    }

    private void OnDisable()
    {
        if (playerHealth != null)
            playerHealth.onDamaged.RemoveListener(OnHit);
    }

    private void Update()
    {
        UpdateFootsteps();
    }

    private void OnHit()
    {
        PlayRandom(hitSounds, hitVolume, ref lastHitIndex);
    }

    public void OnReload()
    {
        if (reloadSound == null)
            return;

        audioSource.PlayOneShot(reloadSound, reloadVolume);
    }

    private void UpdateFootsteps()
    {
        if (playerStatus == null)
            return;

        Vector3 currentPosition = transform.position;
        float moved = Vector3.Distance(
            new Vector3(currentPosition.x, 0f, currentPosition.z),
            new Vector3(lastFootstepPosition.x, 0f, lastFootstepPosition.z)
        );

        bool isGrounded = characterController != null
            ? characterController.isGrounded
            : true;

        if (!isGrounded || moved < minMoveSpeedForFootstep * Time.deltaTime)
        {
            lastFootstepPosition = currentPosition;
            return;
        }

        distanceTraveled += moved;
        lastFootstepPosition = currentPosition;

        if (playerStatus.IsRunning)
        {
            if (distanceTraveled >= runStepDistance)
            {
                PlayRandom(runFootsteps, runFootstepVolume, ref lastFootstepIndex);
                distanceTraveled = 0f;
            }
        }
        else if (playerStatus.IsCrouching)
        {
            if (distanceTraveled >= crouchStepDistance)
            {
                PlayRandom(crouchFootsteps, crouchFootstepVolume, ref lastFootstepIndex);
                distanceTraveled = 0f;
            }
        }
        else
        {
            if (distanceTraveled >= walkStepDistance)
            {
                PlayRandom(walkFootsteps, walkFootstepVolume, ref lastFootstepIndex);
                distanceTraveled = 0f;
            }
        }
    }

    private void PlayRandom(AudioClip[] clips, float volume, ref int lastIndex)
    {
        if (clips == null || clips.Length == 0)
            return;

        int index = clips.Length == 1
            ? 0
            : RandomExcluding(0, clips.Length, lastIndex);

        lastIndex = index;

        AudioClip clip = clips[index];

        if (clip != null)
            audioSource.PlayOneShot(clip, volume);
    }

    private int RandomExcluding(int min, int max, int exclude)
    {
        if (max - min <= 1)
            return min;

        int result = Random.Range(min, max - 1);

        if (result >= exclude)
            result++;

        return result;
    }
}
