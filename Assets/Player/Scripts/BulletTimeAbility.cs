using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class BulletTimeAbility : MonoBehaviour
{
    [Header("Resource")]
    [Min(0.1f)] public float maxResource = 5f;
    [Min(0.01f)] public float drainPerSecond = 1f;
    [Min(0.01f)] public float regenPerSecond = 0.7f;

    [Header("Time Dilation")]
    [Range(0.05f, 1f)] public float timeScale = 0.25f;
    [Min(0.01f)] public float fixedDeltaScale = 1f;

    [Header("Cooldown")]
    [Min(0f)] public float toggleCooldown = 1f;

    [Header("Input")]
    public string actionName = "BulletTime";

    public bool IsActive { get; private set; }
    public float CurrentResource { get; private set; }

    [Header("Player")]
    public PlayerMovement playerMovement;
    private PlayerInput playerInput;
    private InputAction action;
    private float nextToggleTime;
    private float defaultFixedDelta;
    private bool restoringTime;

void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();

        CurrentResource = maxResource;
        defaultFixedDelta = Time.fixedDeltaTime;
    }

    void OnEnable()
    {
        if (playerInput != null)
        {
            action = playerInput.actions[actionName];
            action?.Enable();
            if (action != null)
                action.performed += OnActionPerformed;
        }
    }

    void OnDisable()
    {
        if (action != null)
            action.performed -= OnActionPerformed;

        action?.Disable();
        action = null;

        if (IsActive)
            RestoreTimeScaleImmediate();
    }

    void Update()
    {
        if (IsActive)
        {
            CurrentResource -= drainPerSecond * Time.unscaledDeltaTime;
            if (CurrentResource <= 0f)
            {
                CurrentResource = 0f;
                SetActive(false, force: true);
            }
        }
        else
        {
            CurrentResource += regenPerSecond * Time.unscaledDeltaTime;
            if (CurrentResource > maxResource)
                CurrentResource = maxResource;
        }
    }

    void OnActionPerformed(InputAction.CallbackContext ctx)
    {
        if (Time.unscaledTime < nextToggleTime)
            return;

        bool canEnable = CurrentResource > 0.01f;

        if (!IsActive && !canEnable)
            return;

        SetActive(!IsActive, force: false);
        nextToggleTime = Time.unscaledTime + toggleCooldown;
    }

    void SetActive(bool active, bool force)
    {
        if (IsActive == active && !force)
            return;

        IsActive = active;

        if (IsActive)
        {
            ApplyTimeScale();
        }
        else
        {
            RestoreTimeScaleImmediate();
        }
    }

void ApplyTimeScale()
    {
        float clampedScale = Mathf.Clamp(timeScale, 0.05f, 1f);
        Time.timeScale = clampedScale;
        Time.fixedDeltaTime = defaultFixedDelta * Mathf.Clamp(fixedDeltaScale, 0.01f, 2f) * clampedScale;
        restoringTime = false;

        if (playerMovement != null)
        {
            playerMovement.externalSpeedMultiplier = 1f / clampedScale;
            Animator anim = playerMovement.GetComponentInChildren<Animator>();
            if (anim != null)
                anim.updateMode = AnimatorUpdateMode.UnscaledTime;
        }
    }

void RestoreTimeScaleImmediate()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = defaultFixedDelta;
        restoringTime = true;

        if (playerMovement != null)
        {
            playerMovement.externalSpeedMultiplier = 1f;
            Animator anim = playerMovement.GetComponentInChildren<Animator>();
            if (anim != null)
                anim.updateMode = AnimatorUpdateMode.Normal;
        }
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && IsActive)
        {
            SetActive(false, force: true);
        }
    }
}
