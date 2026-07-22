using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Place on a trigger collider. Shows a prompt and allows scene transition only when:
///   - enemies have not started spawning (Infiltrate state), OR
///   - all waves have been cleared.
/// The interaction effect plays whenever the condition is met, regardless of player proximity.
/// The UI prompt only shows when the player is inside the trigger AND the condition is met.
/// </summary>
[RequireComponent(typeof(Collider))]
public class SceneTransitionTrigger : MonoBehaviour
{
    [Header("Scene")]
    [Tooltip("Name of the scene to load (must be in Build Settings).")]
    [SerializeField] private string targetSceneName;

    [Header("Wave Condition")]
    [Tooltip("Optional — leave empty to always allow interaction.")]
    [SerializeField] private EnemySquadGenerator generator;

    [Header("UI")]
    [Tooltip("TMP_Text in a screen-space canvas shown when the player is inside and can interact.")]
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private string promptMessage = "Press \"E\" to infiltrate deep";

    [Header("Interaction Effect")]
    [Tooltip("ParticleSystem on a child object. Auto-found by name 'interaction effect' if not assigned.")]
    [SerializeField] private ParticleSystem interactionEffect;

    [Header("Input")]
    [SerializeField] private string interactActionName = "Interact";

    private InputAction interactAction;
    private bool playerInside;
    private bool combatStarted;
    private bool allWavesCleared;

    // Interaction is allowed when infiltrate (combat not yet started) OR all waves cleared.
    private bool CanInteract => generator == null || !combatStarted || allWavesCleared;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;

        if (interactionEffect == null)
        {
            Transform child = transform.Find("interaction effect");
            if (child != null)
                interactionEffect = child.GetComponent<ParticleSystem>();

            if (interactionEffect == null)
                interactionEffect = GetComponentInChildren<ParticleSystem>(true);
        }

        SetPromptVisible(false);
    }

    private void Start()
    {
        // Apply initial effect state after all scripts have initialized.
        RefreshAvailability();
    }

    private void OnEnable()
    {
        if (generator == null) return;
        generator.OnStartedGenerating += OnCombatStarted;
        generator.onAllDiedThisRound.AddListener(OnAllWavesCleared);
    }

    private void OnDisable()
    {
        if (generator != null)
        {
            generator.OnStartedGenerating -= OnCombatStarted;
            generator.onAllDiedThisRound.RemoveListener(OnAllWavesCleared);
        }

        LeaveZone();
    }

    // ── Generator events ─────────────────────────────────────────

    private void OnCombatStarted()
    {
        combatStarted   = true;
        allWavesCleared = false;
        RefreshAvailability();
    }

    private void OnAllWavesCleared()
    {
        allWavesCleared = true;
        RefreshAvailability();
    }

    // ── Trigger ───────────────────────────────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        if (playerInside) return;

        PlayerInput pi = GetPlayerInput(other);
        if (pi == null) return;

        interactAction = pi.actions.FindAction(interactActionName, false);
        if (interactAction != null)
            interactAction.performed += OnInteractPerformed;

        playerInside = true;
        RefreshAvailability();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!playerInside) return;
        if (GetPlayerInput(other) == null) return;

        LeaveZone();
    }

    private void OnInteractPerformed(InputAction.CallbackContext _)
    {
        if (!playerInside || !CanInteract) return;

        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogWarning("[SceneTransitionTrigger] Target scene name is empty.", this);
            return;
        }

        GameFlowBootstrapper.AllowNextGameSceneLoad();
        SceneManager.LoadScene(targetSceneName);
    }

    // ── Helpers ───────────────────────────────────────────────────

    private void LeaveZone()
    {
        if (interactAction != null)
        {
            interactAction.performed -= OnInteractPerformed;
            interactAction = null;
        }

        playerInside = false;

        // Only hide the prompt; effect stays based on CanInteract.
        SetPromptVisible(false);
        SetEffectActive(CanInteract);
    }

    private void RefreshAvailability()
    {
        bool canInteract = CanInteract;

        // Effect: visible whenever interaction is available, regardless of player proximity.
        SetEffectActive(canInteract);

        // Prompt: only when the player is also inside the trigger.
        SetPromptVisible(playerInside && canInteract);
    }

    private void SetPromptVisible(bool visible)
    {
        if (promptText == null) return;
        promptText.gameObject.SetActive(visible);
        if (visible) promptText.text = promptMessage;
    }

    private void SetEffectActive(bool active)
    {
        if (interactionEffect == null) return;

        interactionEffect.gameObject.SetActive(active);

        if (active)
            interactionEffect.Play(withChildren: true);
    }

    private static PlayerInput GetPlayerInput(Collider col)
    {
        GameObject root = col.attachedRigidbody != null
            ? col.attachedRigidbody.gameObject
            : col.gameObject;

        return root.GetComponentInParent<PlayerInput>()
            ?? root.GetComponent<PlayerInput>()
            ?? col.GetComponentInParent<PlayerInput>();
    }
}
