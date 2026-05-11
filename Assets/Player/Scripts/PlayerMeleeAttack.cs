using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Animations.Rigging;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInput))]
public class PlayerMeleeAttack : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerWeaponSlots playerWeaponSlots;
    [SerializeField] private ARShootSettings fallbackARShootSettings;
    [SerializeField] private SG_ShootSettings fallbackSGShootSettings;

    [Header("Input")]
    [SerializeField] private string meleeActionName = "Melee";

    [Header("Animator")]
    [SerializeField] private string meleeTriggerName = "Melee";
    [SerializeField] private bool resetTriggerBeforeSet = true;

    [Header("Cooldown")]
    [SerializeField] private bool useMeleeCooldown = true;
    [SerializeField] private float meleeCooldown = 0.6f;

    [Header("Melee Effects")]
    [SerializeField] private MeleeWeaponEffects meleeWeaponEffects;
    [SerializeField] private bool playMeleeEffectOnMeleeInput = true;

    [Header("Melee Damage")]
    [SerializeField] private MeleeDamage meleeDamage;
    [SerializeField] private bool autoFindMeleeDamageInChildren = true;
    [SerializeField] private bool includeInactiveMeleeDamage = true;

    [Header("Melee Rig")]
    [SerializeField] private Rig meleeDisableRig;
    [SerializeField] private float meleeRigWeight = 0f;
    [SerializeField] private bool restoreMeleeRigAfterDuration = true;
    [SerializeField] private float meleeRigDisableDuration = 0.6f;

    [Header("Melee Weapon Visibility")]
    [SerializeField] private bool hideCurrentWeaponDuringMelee = true;
    [SerializeField] private bool showMeleeWeaponDuringMelee = true;
    [SerializeField] private GameObject meleeWeaponObject;
    [SerializeField] private bool restoreWeaponVisibilityAfterDuration = true;
    [SerializeField] private float meleeWeaponVisibleDuration = 0.6f;

    [Header("Shooting Animator Params")]
    [SerializeField] private string shootTriggerName = "Shoot";
    [SerializeField] private string isShootingBoolName = "IsShooting";
    [SerializeField] private string keepShootingBoolName = "KeepShooting";
    [SerializeField] private string quickShotBoolName = "QuickShot";
    [SerializeField] private bool resetShootTriggerWhenMelee = true;

    [Header("Shooting Cancel")]
    [SerializeField] private bool cancelShootingOnMelee = true;
    [SerializeField] private bool blockMeleeWhenShootingIfCannotCancel = true;

    [Header("Locks")]
    [SerializeField] private bool blockWhenMovementLocked = true;
    [SerializeField] private bool blockWhenDiving = true;
    [SerializeField] private bool blockWhenSliding = true;
    [SerializeField] private bool blockWhenRecoveryLocked = true;
    [SerializeField] private bool blockWhenRunning = false;

    private PlayerInput playerInput;
    private InputAction meleeAction;

    private int meleeTriggerHash;
    private int shootTriggerHash;
    private int isShootingBoolHash;
    private int keepShootingBoolHash;
    private int quickShotBoolHash;

    private float nextMeleeAllowedTime = -999f;

    private float cachedMeleeRigWeight = 1f;
    private bool hasCachedMeleeRigWeight = false;
    private Coroutine meleeRigRoutine;

    private GameObject cachedCurrentWeaponObject;
    private bool cachedCurrentWeaponWasActive;
    private bool hasCachedCurrentWeaponObject;
    private Coroutine meleeWeaponVisibilityRoutine;

    public bool IsMeleeOnCooldown =>
        useMeleeCooldown && Time.time < nextMeleeAllowedTime;

    public float MeleeCooldownRemaining =>
        IsMeleeOnCooldown ? nextMeleeAllowedTime - Time.time : 0f;

    private void Reset()
    {
        playerInput = GetComponent<PlayerInput>();
        animator = GetComponentInChildren<Animator>();
        playerMovement = GetComponent<PlayerMovement>();
        playerWeaponSlots = GetComponent<PlayerWeaponSlots>();

        fallbackARShootSettings = GetComponentInChildren<ARShootSettings>(true);
        fallbackSGShootSettings = GetComponentInChildren<SG_ShootSettings>(true);
        meleeWeaponEffects = GetComponentInChildren<MeleeWeaponEffects>(true);

        FindMeleeDamage();
    }

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();

        if (playerWeaponSlots == null)
            playerWeaponSlots = GetComponent<PlayerWeaponSlots>();

        if (meleeWeaponEffects == null)
            meleeWeaponEffects = GetComponentInChildren<MeleeWeaponEffects>(true);

        if (fallbackARShootSettings == null)
            fallbackARShootSettings = GetComponentInChildren<ARShootSettings>(true);

        if (fallbackSGShootSettings == null)
            fallbackSGShootSettings = GetComponentInChildren<SG_ShootSettings>(true);

        if (meleeDamage == null && autoFindMeleeDamageInChildren)
            FindMeleeDamage();

        meleeTriggerHash = Animator.StringToHash(meleeTriggerName);
        shootTriggerHash = Animator.StringToHash(shootTriggerName);
        isShootingBoolHash = Animator.StringToHash(isShootingBoolName);
        keepShootingBoolHash = Animator.StringToHash(keepShootingBoolName);
        quickShotBoolHash = Animator.StringToHash(quickShotBoolName);

        if (meleeWeaponObject != null)
            meleeWeaponObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();

        if (playerInput == null || playerInput.actions == null)
            return;

        meleeAction = playerInput.actions[meleeActionName];

        if (meleeAction != null)
        {
            meleeAction.Enable();
            meleeAction.performed += OnMeleePerformed;
        }
    }

    private void OnDisable()
    {
        if (meleeAction != null)
        {
            meleeAction.performed -= OnMeleePerformed;
            meleeAction.Disable();
        }

        CloseDamageWindow();

        StopMeleeRigRoutineOnly();
        RestoreMeleeRigWeight();

        StopMeleeWeaponVisibilityRoutineOnly();
        RestoreWeaponVisibility();
    }

    private void OnMeleePerformed(InputAction.CallbackContext context)
    {
        TryMeleeAttack();
    }

    public bool TryMeleeAttack()
    {
        if (!CanMeleeAttack())
            return false;

        if (playerMovement != null)
            playerMovement.CancelAimAndRequireRepress();

        if (cancelShootingOnMelee)
            CancelShootingRuntime();

        ApplyMeleeRigWeight();
        ApplyMeleeWeaponVisibility();

        PlayMeleeAnimation();

        if (playMeleeEffectOnMeleeInput && meleeWeaponEffects != null)
            meleeWeaponEffects.PlayMeleeEffectFromMeleeInput();

        StartMeleeCooldown();

        if (restoreMeleeRigAfterDuration)
            StartMeleeRigRestoreRoutine();

        if (restoreWeaponVisibilityAfterDuration)
            StartMeleeWeaponVisibilityRestoreRoutine();

        return true;
    }

    private bool CanMeleeAttack()
    {
        if (animator == null)
            return false;

        if (IsMeleeOnCooldown)
            return false;

        if (playerMovement != null)
        {
            if (blockWhenMovementLocked && playerMovement.externalMovementLock)
                return false;

            if (blockWhenDiving && playerMovement.IsDivingNow)
                return false;

            if (blockWhenSliding && playerMovement.IsSlidingNow)
                return false;

            if (blockWhenRecoveryLocked && playerMovement.IsRecoveryLockedNow)
                return false;

            if (blockWhenRunning && playerMovement.IsRunningNow)
                return false;
        }

        if (IsShootingAnimatorStateActive())
        {
            if (cancelShootingOnMelee)
                return true;

            if (blockMeleeWhenShootingIfCannotCancel)
                return false;
        }

        return true;
    }

    [ContextMenu("Find Melee Damage")]
    public void FindMeleeDamage()
    {
        if (meleeWeaponObject != null)
            meleeDamage = meleeWeaponObject.GetComponentInChildren<MeleeDamage>(includeInactiveMeleeDamage);

        if (meleeDamage == null)
            meleeDamage = GetComponentInChildren<MeleeDamage>(includeInactiveMeleeDamage);
    }

    public void OpenDamageWindow()
    {
        if (meleeDamage == null && autoFindMeleeDamageInChildren)
            FindMeleeDamage();

        if (meleeDamage != null)
            meleeDamage.OpenDamageWindow();
    }

    public void CloseDamageWindow()
    {
        if (meleeDamage == null && autoFindMeleeDamageInChildren)
            FindMeleeDamage();

        if (meleeDamage != null)
            meleeDamage.CloseDamageWindow();
    }

    public void OpenDamageWindowForDefaultDuration()
    {
        if (meleeDamage == null && autoFindMeleeDamageInChildren)
            FindMeleeDamage();

        if (meleeDamage != null)
            meleeDamage.OpenDamageWindowForDefaultDuration();
    }

    public void EnableDamage()
    {
        OpenDamageWindow();
    }

    public void DisableDamage()
    {
        CloseDamageWindow();
    }

    private void CancelShootingRuntime()
    {
        ARShootSettings currentAR = GetCurrentARShootSettings();
        SG_ShootSettings currentSG = GetCurrentSGShootSettings();

        bool clearedCurrentWeapon = false;

        if (currentAR != null)
        {
            currentAR.ForceClearRuntimeState();
            clearedCurrentWeapon = true;
        }

        if (currentSG != null)
        {
            currentSG.ForceClearRuntimeState();
            clearedCurrentWeapon = true;
        }

        if (!clearedCurrentWeapon)
        {
            if (fallbackARShootSettings != null)
                fallbackARShootSettings.ForceClearRuntimeState();

            if (fallbackSGShootSettings != null)
                fallbackSGShootSettings.ForceClearRuntimeState();
        }

        ClearShootingAnimatorState();
    }

    private ARShootSettings GetCurrentARShootSettings()
    {
        if (playerWeaponSlots != null && playerWeaponSlots.CurrentARShootSettings != null)
            return playerWeaponSlots.CurrentARShootSettings;

        return fallbackARShootSettings;
    }

    private SG_ShootSettings GetCurrentSGShootSettings()
    {
        if (playerWeaponSlots != null && playerWeaponSlots.CurrentSGShootSettings != null)
            return playerWeaponSlots.CurrentSGShootSettings;

        return fallbackSGShootSettings;
    }

    private void ClearShootingAnimatorState()
    {
        if (animator == null)
            return;

        if (resetShootTriggerWhenMelee)
            animator.ResetTrigger(shootTriggerHash);

        SetAnimatorBoolIfExists(isShootingBoolName, isShootingBoolHash, false);
        SetAnimatorBoolIfExists(keepShootingBoolName, keepShootingBoolHash, false);
        SetAnimatorBoolIfExists(quickShotBoolName, quickShotBoolHash, false);
    }

    private bool IsShootingAnimatorStateActive()
    {
        if (animator == null)
            return false;

        if (GetAnimatorBoolIfExists(isShootingBoolName, isShootingBoolHash))
            return true;

        if (GetAnimatorBoolIfExists(keepShootingBoolName, keepShootingBoolHash))
            return true;

        if (GetAnimatorBoolIfExists(quickShotBoolName, quickShotBoolHash))
            return true;

        return false;
    }

    private void PlayMeleeAnimation()
    {
        if (animator == null)
            return;

        if (resetTriggerBeforeSet)
            animator.ResetTrigger(meleeTriggerHash);

        animator.SetTrigger(meleeTriggerHash);
    }

    private void StartMeleeCooldown()
    {
        if (!useMeleeCooldown)
            return;

        nextMeleeAllowedTime = Time.time + Mathf.Max(0f, meleeCooldown);
    }

    public void ApplyMeleeRigWeight()
    {
        if (meleeDisableRig == null)
            return;

        if (!hasCachedMeleeRigWeight)
        {
            cachedMeleeRigWeight = meleeDisableRig.weight;
            hasCachedMeleeRigWeight = true;
        }

        meleeDisableRig.weight = meleeRigWeight;
    }

    public void RestoreMeleeRigWeight()
    {
        if (meleeDisableRig == null)
            return;

        if (hasCachedMeleeRigWeight)
        {
            meleeDisableRig.weight = cachedMeleeRigWeight;
            hasCachedMeleeRigWeight = false;
        }
    }

    private void StartMeleeRigRestoreRoutine()
    {
        StopMeleeRigRoutineOnly();
        meleeRigRoutine = StartCoroutine(MeleeRigRestoreRoutine());
    }

    private IEnumerator MeleeRigRestoreRoutine()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, meleeRigDisableDuration));

        RestoreMeleeRigWeight();
        meleeRigRoutine = null;
    }

    private void StopMeleeRigRoutineOnly()
    {
        if (meleeRigRoutine != null)
        {
            StopCoroutine(meleeRigRoutine);
            meleeRigRoutine = null;
        }
    }

    public void ApplyMeleeWeaponVisibility()
    {
        if (hideCurrentWeaponDuringMelee)
        {
            GameObject currentWeaponObject = GetCurrentWeaponObject();

            if (currentWeaponObject != null)
            {
                cachedCurrentWeaponObject = currentWeaponObject;
                cachedCurrentWeaponWasActive = currentWeaponObject.activeSelf;
                hasCachedCurrentWeaponObject = true;

                currentWeaponObject.SetActive(false);
            }
        }

        if (showMeleeWeaponDuringMelee && meleeWeaponObject != null)
            meleeWeaponObject.SetActive(true);
    }

    public void RestoreWeaponVisibility()
    {
        if (showMeleeWeaponDuringMelee && meleeWeaponObject != null)
            meleeWeaponObject.SetActive(false);

        if (hasCachedCurrentWeaponObject && cachedCurrentWeaponObject != null)
            cachedCurrentWeaponObject.SetActive(cachedCurrentWeaponWasActive);

        cachedCurrentWeaponObject = null;
        cachedCurrentWeaponWasActive = false;
        hasCachedCurrentWeaponObject = false;
    }

    private GameObject GetCurrentWeaponObject()
    {
        if (playerWeaponSlots != null && playerWeaponSlots.CurrentWeaponObject != null)
            return playerWeaponSlots.CurrentWeaponObject;

        if (fallbackARShootSettings != null)
            return fallbackARShootSettings.gameObject;

        if (fallbackSGShootSettings != null)
            return fallbackSGShootSettings.gameObject;

        return null;
    }

    private void StartMeleeWeaponVisibilityRestoreRoutine()
    {
        StopMeleeWeaponVisibilityRoutineOnly();
        meleeWeaponVisibilityRoutine = StartCoroutine(MeleeWeaponVisibilityRoutine());
    }

    private IEnumerator MeleeWeaponVisibilityRoutine()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, meleeWeaponVisibleDuration));

        RestoreWeaponVisibility();
        meleeWeaponVisibilityRoutine = null;
    }

    private void StopMeleeWeaponVisibilityRoutineOnly()
    {
        if (meleeWeaponVisibilityRoutine != null)
        {
            StopCoroutine(meleeWeaponVisibilityRoutine);
            meleeWeaponVisibilityRoutine = null;
        }
    }

    private bool GetAnimatorBoolIfExists(string parameterName, int parameterHash)
    {
        if (animator == null)
            return false;

        if (string.IsNullOrWhiteSpace(parameterName))
            return false;

        AnimatorControllerParameter[] parameters = animator.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];

            if (parameter.type == AnimatorControllerParameterType.Bool &&
                parameter.nameHash == parameterHash)
            {
                return animator.GetBool(parameterHash);
            }
        }

        return false;
    }

    private void SetAnimatorBoolIfExists(string parameterName, int parameterHash, bool value)
    {
        if (animator == null)
            return;

        if (string.IsNullOrWhiteSpace(parameterName))
            return;

        AnimatorControllerParameter[] parameters = animator.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];

            if (parameter.type == AnimatorControllerParameterType.Bool &&
                parameter.nameHash == parameterHash)
            {
                animator.SetBool(parameterHash, value);
                return;
            }
        }
    }
}