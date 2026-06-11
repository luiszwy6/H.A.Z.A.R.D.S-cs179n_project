using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Put on the door root. The child trigger zone needs a BossStageDoorZone component.
/// Player enters zone → prompt shown → press E → door slides up.
/// Player leaves zone → door slides back down.
/// </summary>
public class BossStageDoor : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private string promptMessage = "Ready? Press \"E\"";

    [Header("Open Motion")]
    [SerializeField] private float openDistance = 3f;
    [SerializeField] private float openDuration = 1.2f;
    [SerializeField] private float closeDuration = 1.0f;
    [SerializeField] private AnimationCurve openCurve  = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve closeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip closeSound;
    [Range(0f, 1f)]
    [SerializeField] private float soundVolume = 1f;

    [Header("Input")]
    [SerializeField] private string interactActionName = "Interact";

    // Fires once when the player presses E and the door begins opening.
    public event System.Action OnDoorOpened;

    private Vector3 closedPosition;
    private Vector3 openedPosition;

    private InputAction interactAction;
    private bool playerInside;
    private bool isOpen;
    private Coroutine moveCoroutine;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        closedPosition = transform.position;
        openedPosition = closedPosition + Vector3.up * openDistance;
    }

    private void Start()
    {
        SetPromptVisible(false);
    }

    // ── Called by BossStageDoorZone ───────────────────────────────

    public void OnPlayerEntered(PlayerInput playerInput)
    {
        if (playerInside) return;
        playerInside = true;

        interactAction = playerInput.actions.FindAction(interactActionName, false);
        if (interactAction != null)
            interactAction.performed += OnInteractPerformed;

        SetPromptVisible(true);
    }

    public void OnPlayerExited()
    {
        if (!playerInside) return;
        playerInside = false;

        if (interactAction != null)
        {
            interactAction.performed -= OnInteractPerformed;
            interactAction = null;
        }

        SetPromptVisible(false);

        if (isOpen)
            StartMove(opening: false);
    }

    // ── Input ──────────────────────────────────────────────────────

    private void OnInteractPerformed(InputAction.CallbackContext _)
    {
        if (!playerInside || isOpen) return;
        StartMove(opening: true);
        OnDoorOpened?.Invoke();
    }

    // ── Motion ─────────────────────────────────────────────────────

    private void StartMove(bool opening)
    {
        isOpen = opening;

        if (moveCoroutine != null)
            StopCoroutine(moveCoroutine);

        moveCoroutine = StartCoroutine(MoveRoutine(opening));
    }

    private IEnumerator MoveRoutine(bool opening)
    {
        AudioClip clip = opening ? openSound : closeSound;
        if (clip != null && audioSource != null)
            audioSource.PlayOneShot(clip, soundVolume);

        Vector3 from     = transform.position;
        Vector3 to       = opening ? openedPosition : closedPosition;
        float   duration = opening ? openDuration : closeDuration;
        AnimationCurve curve = opening ? openCurve : closeCurve;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = curve.Evaluate(Mathf.Clamp01(elapsed / duration));
            transform.position = Vector3.LerpUnclamped(from, to, t);
            yield return null;
        }

        transform.position = to;
        moveCoroutine = null;
    }

    // ── UI ─────────────────────────────────────────────────────────

    private void SetPromptVisible(bool visible)
    {
        if (promptText == null) return;
        promptText.gameObject.SetActive(visible);
        if (visible) promptText.text = promptMessage;
    }
}
