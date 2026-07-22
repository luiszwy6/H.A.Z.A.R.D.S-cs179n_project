using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public class BulletTimeAbility : MonoBehaviour
{
    [Header("Resource")]
    [Min(0.1f)] public float maxResource = 5f;
    [Min(0.01f)] public float drainPerSecond = 1f;
    [Min(0.01f)] public float regenPerSecond = 0.7f;

    [Header("Time Dilation")]
    [Range(0.05f, 1f)] public float timeScale = 0.25f;
    [Min(0.01f)] public float fixedDeltaScale = 1f;

    [Header("Player Time")]
    public bool playerAffectedByBulletTime = false;

    [Header("Slide Auto Bullet Time")]
    public bool autoActivateDuringSlide = false;
    public bool stopSlideBulletTimeWhenSlideEnds = true;
    public bool playerAffectedDuringSlideBulletTime = true;

    [Header("Cooldown")]
    [Min(0f)] public float toggleCooldown = 1f;

    [Header("Input")]
    public string actionName = "BulletTime";

    [Header("Volume")]
    public Volume postProcessVolume;
    public VolumeProfile normalProfile;
    public VolumeProfile bulletTimeProfile;

    [Header("Priority Coordination")]
    [Tooltip("Drag in SpecialAbilityVolumeManager. When SA is active, SA manages the volume instead.")]
    public SpecialAbilityVolumeManager specialAbilityVolumeManager;

    [Header("Player")]
    public PlayerMovement playerMovement;

    public bool IsActive { get; private set; }
    public float CurrentResource { get; private set; }

    public bool ManualRequested => manualRequested;
    public bool SlideRequested => slideRequested;

    public void SetExternalOverride(bool active)
    {
        externalOverride = active;
        RefreshActiveState(force: true);
    }

    private PlayerInput playerInput;
    private InputAction action;

    private float nextToggleTime;
    private float defaultFixedDelta;

    private bool manualRequested;
    private bool slideRequested;
    private bool slideAutoBlockedUntilSlideEnds;
    private bool externalOverride;

    private bool lastAppliedPlayerAffected;
    private bool hasAppliedTimeScale;

    private Animator playerAnimator;
    private AnimatorUpdateMode defaultAnimatorUpdateMode;
    private bool hasCachedAnimatorUpdateMode;

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();

        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();

        if (specialAbilityVolumeManager == null)
            specialAbilityVolumeManager = FindFirstObjectByType<SpecialAbilityVolumeManager>(FindObjectsInactive.Include);

        CachePlayerAnimator();

        CurrentResource = maxResource;
        defaultFixedDelta = Time.fixedDeltaTime;
    }

    private void OnEnable()
    {
        if (playerInput != null && playerInput.actions != null && !string.IsNullOrWhiteSpace(actionName))
        {
            action = playerInput.actions.FindAction(actionName, false);

            if (action != null)
            {
                action.Enable();
                action.performed += OnActionPerformed;
            }
        }

        RefreshActiveState(force: true);
    }

    private void OnDisable()
    {
        if (action != null)
            action.performed -= OnActionPerformed;

        action?.Disable();
        action = null;

        manualRequested = false;
        slideRequested = false;
        slideAutoBlockedUntilSlideEnds = false;

        RestoreTimeScaleImmediate();
    }

    private void Update()
    {
        UpdateSlideAutoBulletTime();

        RefreshActiveState(force: false);

        if (IsActive)
        {
            if (!externalOverride)
            {
                CurrentResource -= drainPerSecond * Time.unscaledDeltaTime;

                if (CurrentResource <= 0f)
                {
                    CurrentResource = 0f;
                    manualRequested = false;
                    slideRequested = false;

                    if (IsPlayerSliding())
                        slideAutoBlockedUntilSlideEnds = true;

                    RefreshActiveState(force: true);
                    return;
                }
            }

            RefreshActiveState(force: false);
        }
        else
        {
            CurrentResource += regenPerSecond * Time.unscaledDeltaTime;

            if (CurrentResource > maxResource)
                CurrentResource = maxResource;
        }
    }

    private void OnActionPerformed(InputAction.CallbackContext ctx)
    {
        if (Time.unscaledTime < nextToggleTime)
            return;

        bool isActive = manualRequested || slideRequested;

        if (!isActive && CurrentResource <= 0.01f)
            return;

        if (isActive)
        {
            manualRequested = false;

            if (slideRequested)
            {
                slideRequested = false;
                slideAutoBlockedUntilSlideEnds = true;
            }
        }
        else
        {
            manualRequested = true;
        }

        nextToggleTime = Time.unscaledTime + toggleCooldown;
        RefreshActiveState(force: true);
    }

    private void UpdateSlideAutoBulletTime()
    {
        if (!autoActivateDuringSlide)
        {
            slideRequested = false;
            slideAutoBlockedUntilSlideEnds = false;
            return;
        }

        bool sliding = IsPlayerSliding();

        if (!sliding)
        {
            if (stopSlideBulletTimeWhenSlideEnds)
                slideRequested = false;

            slideAutoBlockedUntilSlideEnds = false;
            return;
        }

        if (slideAutoBlockedUntilSlideEnds)
            return;

        if (CurrentResource > 0.01f)
            slideRequested = true;
    }

    private void RefreshActiveState(bool force)
    {
        bool shouldBeActive =
            externalOverride ||
            (CurrentResource > 0.01f && (manualRequested || slideRequested));

        if (!shouldBeActive)
        {
            if (IsActive || force)
            {
                IsActive = false;
                RestoreTimeScaleImmediate();
            }

            return;
        }

        bool playerAffected = ShouldPlayerBeAffectedNow();

        if (!IsActive || force || !hasAppliedTimeScale || playerAffected != lastAppliedPlayerAffected)
        {
            IsActive = true;
            ApplyTimeScale(playerAffected);
        }
    }

    private bool ShouldPlayerBeAffectedNow()
    {
        if (slideRequested &&
            autoActivateDuringSlide &&
            playerAffectedDuringSlideBulletTime &&
            IsPlayerSliding())
        {
            return true;
        }

        return playerAffectedByBulletTime;
    }

    private bool IsPlayerSliding()
    {
        return playerMovement != null && playerMovement.IsSlidingNow;
    }

    private void ApplyTimeScale(bool playerAffected)
    {
        float clampedScale = Mathf.Clamp(timeScale, 0.05f, 1f);

        Time.timeScale = clampedScale;
        Time.fixedDeltaTime =
            defaultFixedDelta *
            Mathf.Clamp(fixedDeltaScale, 0.01f, 2f) *
            clampedScale;

        ApplyPlayerTimeMode(playerAffected, clampedScale);

        lastAppliedPlayerAffected = playerAffected;
        hasAppliedTimeScale = true;

        ApplyVolumeProfile(active: true);
    }

    private void ApplyPlayerTimeMode(bool playerAffected, float clampedScale)
    {
        if (playerMovement == null)
            return;

        CachePlayerAnimator();

        if (playerAffected)
        {
            playerMovement.externalSpeedMultiplier = 1f;

            if (playerAnimator != null && hasCachedAnimatorUpdateMode)
                playerAnimator.updateMode = defaultAnimatorUpdateMode;

            return;
        }

        playerMovement.externalSpeedMultiplier = 1f / clampedScale;

        if (playerAnimator != null)
            playerAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
    }

    private void RestoreTimeScaleImmediate()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = defaultFixedDelta;

        if (playerMovement != null)
            playerMovement.externalSpeedMultiplier = 1f;

        if (playerAnimator != null && hasCachedAnimatorUpdateMode)
            playerAnimator.updateMode = defaultAnimatorUpdateMode;

        hasAppliedTimeScale = false;

        ApplyVolumeProfile(active: false);
    }

    private void ApplyVolumeProfile(bool active)
    {
        if (postProcessVolume == null) return;

        // SpecialAbilityVolumeManager owns all volume priorities (SA > BT > NV > Normal).
        // Let it handle every transition so BT Enable/Disable can't clobber NightVision.
        if (specialAbilityVolumeManager != null)
            return;

        VolumeProfile target = active ? bulletTimeProfile : normalProfile;
        if (target != null)
            postProcessVolume.profile = target;
    }

    private void CachePlayerAnimator()
    {
        if (playerMovement == null)
            return;

        if (playerAnimator == null)
            playerAnimator = playerMovement.GetComponentInChildren<Animator>();

        if (playerAnimator != null && !hasCachedAnimatorUpdateMode)
        {
            defaultAnimatorUpdateMode = playerAnimator.updateMode;
            hasCachedAnimatorUpdateMode = true;
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
            return;

        manualRequested = false;
        slideRequested = false;
        slideAutoBlockedUntilSlideEnds = false;

        if (IsActive)
        {
            IsActive = false;
            RestoreTimeScaleImmediate();
        }
    }
}