using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class ZombieSounds : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ZombieStatus zombieStatus;
    [SerializeField] private AudioSource audioSource;

    [Header("Idle Sounds")]
    [SerializeField] private AudioClip[] idleClips;
    [Range(0f, 1f)]
    [SerializeField] private float volume = 1f;

    [Header("Interval")]
    [Min(0.5f)]
    [SerializeField] private float intervalBase = 10f;
    [Tooltip("Random variance added to each interval. Actual interval = base ± variance.")]
    [Min(0f)]
    [SerializeField] private float intervalVariance = 3f;

    [Header("Pitch")]
    [SerializeField] private bool randomizePitch = true;
    [SerializeField] private float pitchMin = 0.9f;
    [SerializeField] private float pitchMax = 1.1f;

    private Coroutine soundRoutine;

    private void Awake()
    {
        if (zombieStatus == null) zombieStatus = GetComponent<ZombieStatus>();

        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
    }

    private void OnEnable()
    {
        if (soundRoutine != null) StopCoroutine(soundRoutine);
        soundRoutine = StartCoroutine(SoundRoutine());
    }

    private void OnDisable()
    {
        if (soundRoutine != null)
        {
            StopCoroutine(soundRoutine);
            soundRoutine = null;
        }
    }

    private IEnumerator SoundRoutine()
    {
        // Stagger startup so multiple zombies don't all play at once.
        yield return new WaitForSeconds(Random.Range(0f, intervalBase));

        while (true)
        {
            if (zombieStatus == null || !zombieStatus.IsDead)
                PlayRandomClip();

            float wait = intervalBase + Random.Range(-intervalVariance, intervalVariance);
            yield return new WaitForSeconds(Mathf.Max(0.5f, wait));
        }
    }

    private void PlayRandomClip()
    {
        if (idleClips == null || idleClips.Length == 0) return;

        AudioClip clip = null;
        for (int i = 0; i < 8; i++)
        {
            clip = idleClips[Random.Range(0, idleClips.Length)];
            if (clip != null) break;
        }

        if (clip == null) return;

        audioSource.pitch = randomizePitch
            ? Random.Range(Mathf.Min(pitchMin, pitchMax), Mathf.Max(pitchMin, pitchMax))
            : 1f;

        audioSource.PlayOneShot(clip, volume);
    }

}
