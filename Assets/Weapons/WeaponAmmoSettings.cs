using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Animations.Rigging;

[DisallowMultipleComponent]
public class WeaponAmmoSettings : MonoBehaviour
{
    public enum ReloadMode
    {
        Magazine,
        OneByOne
    }

    [Header("Refs")]
    [SerializeField] private TotalAmmoSetter totalAmmoSetter;
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private List<Rig> reloadDisableRigs = new List<Rig>();


    [Header("Ammo Settings")]
    [SerializeField] private TotalAmmoSetter.AmmoType ammoType = TotalAmmoSetter.AmmoType.AssaultRifle;
    [Min(1)] [SerializeField] private int magazineSize = 30;
    [Min(0)] [SerializeField] private int currentAmmoInMagazine = 30;

    [Header("Reload Settings")]
    [SerializeField] private ReloadMode reloadMode = ReloadMode.Magazine;

    [Header("Magazine Reload")]
    [Min(0.01f)] [SerializeField] private float magazineReloadDuration = 1.2f;

    [Header("One By One Reload")]
    [Min(0.01f)] [SerializeField] private float oneByOneReloadDurationPerRound = 0.55f;

    [Tooltip("Small off-gap after each one-by-one reload round so Animator can read IsReloading false before the next round starts.")]
    [Min(0f)] [SerializeField] private float oneByOneReloadBoolOffGap = 0.03f;

    [Tooltip("If true, shooting can cancel one-by-one reload only when the magazine already has at least one round.")]
    [SerializeField] private bool allowShootCancelOneByOneReload = true;

    [Header("Reload Animator")]
    [SerializeField] private string reloadActionName = "Reload";
    [SerializeField] private string reloadTriggerName = "Reload";
    [SerializeField] private string isReloadingBoolName = "IsReloading";

    [Tooltip("For one-by-one reload. This stays true during the whole continuous reload process, even while IsReloading pulses true/false per shell.")]
    [SerializeField] private string keepReloadingBoolName = "KeepReloading";

    [SerializeField] private bool resetTriggerBeforeSet = true;
    [SerializeField] private float reloadRigWeight = 0f;

    private InputAction reloadAction;
    private int reloadTriggerHash;
    private int isReloadingBoolHash;
    private int keepReloadingBoolHash;
    private Coroutine reloadRoutine;

    private bool isReloading;

    private readonly Dictionary<Rig, float> cachedRigWeights = new Dictionary<Rig, float>();

    public TotalAmmoSetter.AmmoType AmmoType => ammoType;
    public ReloadMode CurrentReloadMode => reloadMode;

    public void AddReserveAmmo(int amount)
    {
        if (amount <= 0 || totalAmmoSetter == null)
            return;

        totalAmmoSetter.AddAmmo(ammoType, amount);
    }

    public int MagazineSize => magazineSize;
    public int CurrentAmmoInMagazine => currentAmmoInMagazine;
    public int CurrentReserveAmmo => totalAmmoSetter != null ? totalAmmoSetter.GetAmmoCount(ammoType) : 0;
    public bool IsReloading => isReloading;
    public bool IsMagazineFull => currentAmmoInMagazine >= magazineSize;

    private void Reset()
    {
        Transform root = transform.root;

        if (playerInput == null)
            playerInput = root.GetComponent<PlayerInput>();

        if (animator == null)
            animator = root.GetComponentInChildren<Animator>();

        if (playerMovement == null)
            playerMovement = root.GetComponent<PlayerMovement>();

        if (totalAmmoSetter == null)
            totalAmmoSetter = FindFirstObjectByType<TotalAmmoSetter>();
    }

    private void OnValidate()
    {
        magazineSize = Mathf.Max(1, magazineSize);
        currentAmmoInMagazine = Mathf.Clamp(currentAmmoInMagazine, 0, magazineSize);

        magazineReloadDuration = Mathf.Max(0.01f, magazineReloadDuration);
        oneByOneReloadDurationPerRound = Mathf.Max(0.01f, oneByOneReloadDurationPerRound);
        oneByOneReloadBoolOffGap = Mathf.Max(0f, oneByOneReloadBoolOffGap);
    }

    private void Awake()
    {
        Transform root = transform.root;

        if (playerInput == null)
            playerInput = root.GetComponent<PlayerInput>();

        if (animator == null)
            animator = root.GetComponentInChildren<Animator>();

        if (playerMovement == null)
            playerMovement = root.GetComponent<PlayerMovement>();

        if (totalAmmoSetter == null)
            totalAmmoSetter = FindFirstObjectByType<TotalAmmoSetter>();

        currentAmmoInMagazine = Mathf.Clamp(currentAmmoInMagazine, 0, magazineSize);

        if (playerInput != null && playerInput.actions != null)
            reloadAction = playerInput.actions[reloadActionName];

        reloadTriggerHash = Animator.StringToHash(reloadTriggerName);
        isReloadingBoolHash = Animator.StringToHash(isReloadingBoolName);
        keepReloadingBoolHash = Animator.StringToHash(keepReloadingBoolName);
    }

    private void OnEnable()
    {
        ForceClearReloadState();
    }

    private void OnDisable()
    {
        StopReloadRoutineOnly();
        ForceClearReloadState();
    }

    private void Update()
    {
        if (reloadAction != null && reloadAction.WasPressedThisFrame())
            TryStartReload();
    }

    public bool CanShoot()
    {
        return !isReloading && currentAmmoInMagazine > 0;
    }

    public bool TryCancelReloadForShoot()
    {
        if (!isReloading)
            return currentAmmoInMagazine > 0;

        if (!allowShootCancelOneByOneReload)
            return false;

        if (reloadMode != ReloadMode.OneByOne)
            return false;

        if (currentAmmoInMagazine <= 0)
            return false;

        CancelReload();
        return true;
    }

    public bool TryConsumeOneRound()
    {
        if (currentAmmoInMagazine <= 0)
            return false;

        currentAmmoInMagazine--;
        return true;
    }

    public bool CanReload()
    {
        if (isReloading)
            return false;

        if (currentAmmoInMagazine >= magazineSize)
            return false;

        if (totalAmmoSetter == null || !totalAmmoSetter.HasAmmo(ammoType))
            return false;

        if (playerMovement != null)
        {
            if (playerMovement.IsDivingNow || playerMovement.IsSlidingNow)
                return false;
        }

        return true;
    }

    public bool TryStartReload()
    {
        if (!CanReload())
            return false;

        StopReloadRoutineOnly();

        if (reloadMode == ReloadMode.OneByOne)
            reloadRoutine = StartCoroutine(OneByOneReloadRoutine());
        else
            reloadRoutine = StartCoroutine(MagazineReloadRoutine());

        return true;
    }

    public void CancelReload()
    {
        StopReloadRoutineOnly();
        ForceClearReloadState();
    }

    public void ForceClearReloadState()
    {
        isReloading = false;
        RestoreRigWeights();

        SetAnimatorReloadingBool(false);
        SetAnimatorKeepReloadingBool(false);
    }

    private IEnumerator MagazineReloadRoutine()
    {
        BeginReloadProcess(keepReloadingInAnimator: false);

        SetAnimatorReloadingBool(true);
        PlayReloadAnimationTrigger();

        yield return new WaitForSecondsRealtime(magazineReloadDuration);

        int need = Mathf.Max(0, magazineSize - currentAmmoInMagazine);
        int loaded = totalAmmoSetter != null ? totalAmmoSetter.ConsumeAmmo(ammoType, need) : 0;

        currentAmmoInMagazine += loaded;
        currentAmmoInMagazine = Mathf.Clamp(currentAmmoInMagazine, 0, magazineSize);

        EndReloadProcess();
        reloadRoutine = null;
    }

    private IEnumerator OneByOneReloadRoutine()
    {
        BeginReloadProcess(keepReloadingInAnimator: true);

        while (currentAmmoInMagazine < magazineSize &&
               totalAmmoSetter != null &&
               totalAmmoSetter.HasAmmo(ammoType))
        {
            SetAnimatorReloadingBool(true);
            PlayReloadAnimationTrigger();

            yield return new WaitForSecondsRealtime(oneByOneReloadDurationPerRound);

            int loaded = totalAmmoSetter.ConsumeAmmo(ammoType, 1);

            if (loaded <= 0)
                break;

            currentAmmoInMagazine += loaded;
            currentAmmoInMagazine = Mathf.Clamp(currentAmmoInMagazine, 0, magazineSize);

            SetAnimatorReloadingBool(false);

            if (currentAmmoInMagazine >= magazineSize)
                break;

            if (totalAmmoSetter == null || !totalAmmoSetter.HasAmmo(ammoType))
                break;

            if (oneByOneReloadBoolOffGap > 0f)
                yield return new WaitForSecondsRealtime(oneByOneReloadBoolOffGap);
            else
                yield return null;
        }

        EndReloadProcess();
        reloadRoutine = null;
    }

    private void BeginReloadProcess(bool keepReloadingInAnimator)
    {
        isReloading = true;

        if (playerMovement != null)
            playerMovement.CancelAimAndRequireRepress();

        ApplyReloadRigWeights();

        SetAnimatorKeepReloadingBool(keepReloadingInAnimator);
    }

    private void EndReloadProcess()
    {
        isReloading = false;
        RestoreRigWeights();

        SetAnimatorReloadingBool(false);
        SetAnimatorKeepReloadingBool(false);
    }

    private void SetAnimatorReloadingBool(bool value)
    {
        if (animator == null)
            return;

        animator.SetBool(isReloadingBoolHash, value);
    }

    private void SetAnimatorKeepReloadingBool(bool value)
    {
        if (animator == null)
            return;

        if (string.IsNullOrWhiteSpace(keepReloadingBoolName))
            return;

        animator.SetBool(keepReloadingBoolHash, value);
    }

    private void PlayReloadAnimationTrigger()
    {
        if (animator == null)
            return;

        if (resetTriggerBeforeSet)
            animator.ResetTrigger(reloadTriggerHash);

        animator.SetTrigger(reloadTriggerHash);
    }

    private void StopReloadRoutineOnly()
    {
        if (reloadRoutine != null)
        {
            StopCoroutine(reloadRoutine);
            reloadRoutine = null;
        }
    }

    private void ApplyReloadRigWeights()
    {
        if (reloadDisableRigs == null)
            return;

        for (int i = 0; i < reloadDisableRigs.Count; i++)
        {
            Rig rig = reloadDisableRigs[i];

            if (rig == null)
                continue;

            if (!cachedRigWeights.ContainsKey(rig))
                cachedRigWeights.Add(rig, rig.weight);

            rig.weight = reloadRigWeight;
        }
    }

    private void RestoreRigWeights()
    {
        if (cachedRigWeights.Count == 0)
            return;

        foreach (KeyValuePair<Rig, float> pair in cachedRigWeights)
        {
            if (pair.Key == null)
                continue;

            pair.Key.weight = pair.Value;
        }

        cachedRigWeights.Clear();
    }
}