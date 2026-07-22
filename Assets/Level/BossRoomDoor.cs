using System.Collections;
using UnityEngine;

public class BossRoomDoor : MonoBehaviour
{
    [Header("Wave Condition")]
    [Tooltip("The generator whose spawn events drive the door.")]
    [SerializeField] private EnemySquadGenerator generator;

    [Header("Motion")]
    [Tooltip("How far (metres) the door moves upward when fully open.")]
    [SerializeField] private float openDistance = 3f;
    [Tooltip("Seconds taken to slide fully open.")]
    [SerializeField] private float openDuration = 1.5f;
    [Tooltip("Seconds taken to slide fully closed.")]
    [SerializeField] private float closeDuration = 1.5f;
    [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip closeSound;
    [Range(0f, 1f)]
    [SerializeField] private float soundVolume = 1f;

    private Vector3 closedPosition;
    private Vector3 openedPosition;
    private bool isOpen = true;
    private Coroutine moveRoutine;

    private void Awake()
    {
        if (generator == null)
            generator = FindFirstObjectByType<EnemySquadGenerator>();
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        closedPosition = transform.position;
        openedPosition = closedPosition + Vector3.up * openDistance;
    }

    private void Start()
    {
        // Door begins open
        transform.position = openedPosition;
    }

    private void OnEnable()
    {
        if (generator != null)
        {
            generator.OnStartedGenerating += OnSpawnStarted;
            generator.onAllDiedThisRound.AddListener(OnAllWavesCleared);
        }
    }

    private void OnDisable()
    {
        if (generator != null)
        {
            generator.OnStartedGenerating -= OnSpawnStarted;
            generator.onAllDiedThisRound.RemoveListener(OnAllWavesCleared);
        }
    }

    private void OnSpawnStarted()
    {
        if (!isOpen) return;
        isOpen = false;
        StartMove(openedPosition, closedPosition, closeDuration, closeSound);
    }

    private void OnAllWavesCleared()
    {
        if (isOpen) return;
        isOpen = true;
        StartMove(closedPosition, openedPosition, openDuration, openSound);
    }

    private void StartMove(Vector3 from, Vector3 to, float duration, AudioClip clip)
    {
        if (moveRoutine != null) StopCoroutine(moveRoutine);
        moveRoutine = StartCoroutine(MoveRoutine(from, to, duration, clip));
    }

    private IEnumerator MoveRoutine(Vector3 from, Vector3 to, float duration, AudioClip clip)
    {
        if (clip != null && audioSource != null)
            audioSource.PlayOneShot(clip, soundVolume);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = moveCurve.Evaluate(Mathf.Clamp01(elapsed / duration));
            transform.position = Vector3.LerpUnclamped(from, to, t);
            yield return null;
        }

        transform.position = to;
        moveRoutine = null;
    }
}
