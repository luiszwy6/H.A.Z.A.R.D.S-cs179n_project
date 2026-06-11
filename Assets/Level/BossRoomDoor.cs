using System.Collections;
using UnityEngine;

public class BossRoomDoor : MonoBehaviour
{
    [Header("Wave Condition")]
    [Tooltip("The generator whose onAllDiedThisRound event triggers the door.")]
    [SerializeField] private EnemySquadGenerator generator;

    [Header("Open Motion")]
    [Tooltip("How far (metres) the door moves upward when fully open.")]
    [SerializeField] private float openDistance = 3f;
    [Tooltip("Seconds taken to slide fully open.")]
    [SerializeField] private float openDuration = 1.5f;
    [SerializeField] private AnimationCurve openCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip openSound;
    [Range(0f, 1f)]
    [SerializeField] private float soundVolume = 1f;

    private bool isOpen;

    private void Awake()
    {
        if (generator == null)
            generator = FindFirstObjectByType<EnemySquadGenerator>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    private void OnEnable()
    {
        if (generator != null)
            generator.onAllDiedThisRound.AddListener(OnAllWavesCleared);
    }

    private void OnDisable()
    {
        if (generator != null)
            generator.onAllDiedThisRound.RemoveListener(OnAllWavesCleared);
    }

    private void OnAllWavesCleared()
    {
        if (isOpen) return;
        isOpen = true;
        StartCoroutine(OpenRoutine());
    }

    private IEnumerator OpenRoutine()
    {
        if (openSound != null && audioSource != null)
            audioSource.PlayOneShot(openSound, soundVolume);

        Vector3 startPos = transform.position;
        Vector3 endPos = startPos + Vector3.up * openDistance;
        float elapsed = 0f;

        while (elapsed < openDuration)
        {
            elapsed += Time.deltaTime;
            float t = openCurve.Evaluate(Mathf.Clamp01(elapsed / openDuration));
            transform.position = Vector3.LerpUnclamped(startPos, endPos, t);
            yield return null;
        }

        transform.position = endPos;
    }
}
