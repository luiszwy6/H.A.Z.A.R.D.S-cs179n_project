using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Animations.Rigging;

public class AssaultRifleAmmoSettings : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private TotalAmmoSetter totalAmmoSetter;
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private Rig reloadDisableRig;

    [Header("Ammo Settings")]
    [SerializeField] private TotalAmmoSetter.AmmoType ammoType = TotalAmmoSetter.AmmoType.AssaultRifle;
    [Min(1)] [SerializeField] private int magazineSize = 30;
    [Min(0)] [SerializeField] private int currentAmmoInMagazine = 30;

    [Header("Reload")]
    [Min(0.01f)] [SerializeField] private float reloadDuration = 1.2f;
    [SerializeField] private string reloadActionName = "Reload";
    [SerializeField] private string reloadTriggerName = "Reload";
    [SerializeField] private string isReloadingBoolName = "IsReloading";
    [SerializeField] private bool resetTriggerBeforeSet = true;
    [SerializeField] private float reloadRigWeight = 0f;

    private InputAction reloadAction;
    private int reloadTriggerHash;
    private int isReloadingBoolHash;
    private Coroutine reloadRoutine;
    private bool isReloading;

    private float cachedRigWeight = 1f;
    private bool hasCachedRigWeight = false;

    public int MagazineSize => magazineSize;
    public int CurrentAmmoInMagazine => currentAmmoInMagazine;
    public int CurrentReserveAmmo => totalAmmoSetter != null ? totalAmmoSetter.GetAmmoCount(ammoType) : 0;
    public bool IsReloading => isReloading;
    public bool IsMagazineFull => currentAmmoInMagazine >= magazineSize;

    private void Reset()
    {
        if (playerInput == null) playerInput = GetComponent<PlayerInput>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (playerMovement == null) playerMovement = GetComponent<PlayerMovement>();
        if (totalAmmoSetter == null) totalAmmoSetter = FindFirstObjectByType<TotalAmmoSetter>();
    }

    private void Awake()
    {
        if (playerInput == null) playerInput = GetComponent<PlayerInput>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (playerMovement == null) playerMovement = GetComponent<PlayerMovement>();
        if (totalAmmoSetter == null) totalAmmoSetter = FindFirstObjectByType<TotalAmmoSetter>();

        currentAmmoInMagazine = Mathf.Clamp(currentAmmoInMagazine, 0, magazineSize);

        if (playerInput != null && playerInput.actions != null)
            reloadAction = playerInput.actions[reloadActionName];

        reloadTriggerHash = Animator.StringToHash(reloadTriggerName);
        isReloadingBoolHash = Animator.StringToHash(isReloadingBoolName);
    }

    private void OnEnable()
    {
        if (reloadAction != null)
            reloadAction.Enable();

        if (animator != null)
            animator.SetBool(isReloadingBoolHash, false);

        isReloading = false;
        RestoreRigWeight();
    }

    private void OnDisable()
    {
        if (reloadAction != null)
            reloadAction.Disable();

        if (reloadRoutine != null)
        {
            StopCoroutine(reloadRoutine);
            reloadRoutine = null;
        }

        isReloading = false;
        RestoreRigWeight();

        if (animator != null)
            animator.SetBool(isReloadingBoolHash, false);
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

    public bool TryConsumeOneRound()
    {
        if (!CanShoot())
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

        if (reloadRoutine != null)
            StopCoroutine(reloadRoutine);

        reloadRoutine = StartCoroutine(ReloadRoutine());
        return true;
    }

    private IEnumerator ReloadRoutine()
    {
        isReloading = true;
        ApplyReloadRigWeight();

        if (animator != null)
        {
            if (resetTriggerBeforeSet)
                animator.ResetTrigger(reloadTriggerHash);

            animator.SetTrigger(reloadTriggerHash);
            animator.SetBool(isReloadingBoolHash, true);
        }

        yield return new WaitForSeconds(reloadDuration);

        int need = Mathf.Max(0, magazineSize - currentAmmoInMagazine);
        int loaded = totalAmmoSetter != null ? totalAmmoSetter.ConsumeAmmo(ammoType, need) : 0;
        currentAmmoInMagazine += loaded;
        currentAmmoInMagazine = Mathf.Clamp(currentAmmoInMagazine, 0, magazineSize);

        isReloading = false;
        RestoreRigWeight();

        if (animator != null)
            animator.SetBool(isReloadingBoolHash, false);

        reloadRoutine = null;
    }

    private void ApplyReloadRigWeight()
    {
        if (reloadDisableRig == null)
            return;

        if (!hasCachedRigWeight)
        {
            cachedRigWeight = reloadDisableRig.weight;
            hasCachedRigWeight = true;
        }

        reloadDisableRig.weight = reloadRigWeight;
    }

    private void RestoreRigWeight()
    {
        if (reloadDisableRig == null)
            return;

        if (hasCachedRigWeight)
        {
            reloadDisableRig.weight = cachedRigWeight;
            hasCachedRigWeight = false;
        }
    }
}