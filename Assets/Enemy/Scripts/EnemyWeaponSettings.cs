using System.Collections;
using UnityEngine;
using UnityEngine.Animations.Rigging;

[DisallowMultipleComponent]
public class EnemyWeaponSettings : MonoBehaviour
{
    public enum EnemyReloadMode
    {
        Magazine,
        OneByOne
    }

    [Header("Refs")]
    [SerializeField] private TotalAmmoSetter enemyTotalAmmoSetter;
    [SerializeField] private Animator enemyAnimator;
    [SerializeField] private Rig reloadDisableRig;
    [SerializeField] private EnemyStatus enemyStatus;
    [SerializeField] private EnemyWeaponAnimatorState weaponAnimatorState;

    [Header("Ammo Settings")]
    [SerializeField] private bool usesAmmoAndReload = true;
    [SerializeField] private TotalAmmoSetter.AmmoType ammoType = TotalAmmoSetter.AmmoType.AssaultRifle;
    [Min(1)] [SerializeField] private int magazineSize = 30;
    [Min(0)] [SerializeField] private int currentAmmoInMagazine = 30;

    [Header("Reload Settings")]
    [SerializeField] private EnemyReloadMode reloadMode = EnemyReloadMode.Magazine;

    [Header("Magazine Reload")]
    [Min(0.01f)] [SerializeField] private float magazineReloadDuration = 1.2f;

    [Header("One By One Reload")]
    [Min(0.01f)] [SerializeField] private float oneByOneReloadDurationPerRound = 0.55f;
    [Min(0f)] [SerializeField] private float oneByOneReloadBoolOffGap = 0.03f;
    [SerializeField] private bool allowShootCancelOneByOneReload = true;

    [Header("Reload Animator Parameters")]
    [SerializeField] private string reloadTriggerName = "Reload";
    [SerializeField] private string isReloadingBoolName = "IsReloading";
    [SerializeField] private string keepReloadingBoolName = "KeepReloading";
    [SerializeField] private bool resetTriggerBeforeSet = true;
    [SerializeField] private bool applyWeaponAnimatorStateBeforeReload = true;

    [Header("Force Reload State")]
    [SerializeField] private bool forceReloadStateByName = false;
    [SerializeField] private string magazineReloadStateName = "Reload";
    [SerializeField] private string oneByOneReloadStateName = "Reload";
    [SerializeField] private int reloadLayerIndex = 0;
    [Min(0f)] [SerializeField] private float reloadCrossFadeDuration = 0.05f;

    [Header("Rig During Reload")]
    [SerializeField] private float reloadRigWeight = 0f;

    private int reloadTriggerHash;
    private int isReloadingBoolHash;
    private int keepReloadingBoolHash;

    private Coroutine reloadRoutine;
    private bool isReloading;

    private float cachedRigWeight = 1f;
    private bool hasCachedRigWeight;

    public TotalAmmoSetter.AmmoType AmmoType
    {
        get { return ammoType; }
    }

    public EnemyReloadMode ReloadMode
    {
        get { return reloadMode; }
    }

    public bool UsesAmmoAndReload
    {
        get { return usesAmmoAndReload; }
    }

    public int MagazineSize
    {
        get { return magazineSize; }
    }

    public int CurrentAmmoInMagazine
    {
        get { return usesAmmoAndReload ? currentAmmoInMagazine : magazineSize; }
    }

    public int CurrentReserveAmmo
    {
        get
        {
            if (!usesAmmoAndReload)
                return 0;

            if (enemyTotalAmmoSetter == null)
                return 0;

            return enemyTotalAmmoSetter.GetAmmoCount(ammoType);
        }
    }

    public bool IsReloading
    {
        get { return usesAmmoAndReload && isReloading; }
    }

    public bool IsMagazineFull
    {
        get { return !usesAmmoAndReload || currentAmmoInMagazine >= magazineSize; }
    }

    public bool IsMagazineEmpty
    {
        get { return usesAmmoAndReload && currentAmmoInMagazine <= 0; }
    }

    public bool HasReserveAmmo
    {
        get
        {
            return usesAmmoAndReload && enemyTotalAmmoSetter != null && enemyTotalAmmoSetter.HasAmmo(ammoType);
        }
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        magazineSize = Mathf.Max(1, magazineSize);
        currentAmmoInMagazine = Mathf.Clamp(currentAmmoInMagazine, 0, magazineSize);

        magazineReloadDuration = Mathf.Max(0.01f, magazineReloadDuration);
        oneByOneReloadDurationPerRound = Mathf.Max(0.01f, oneByOneReloadDurationPerRound);
        oneByOneReloadBoolOffGap = Mathf.Max(0f, oneByOneReloadBoolOffGap);
        reloadLayerIndex = Mathf.Max(0, reloadLayerIndex);
    }

    private void Awake()
    {
        ResolveReferences();

        currentAmmoInMagazine = Mathf.Clamp(currentAmmoInMagazine, 0, magazineSize);

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

    private void ResolveReferences()
    {
        Transform root = transform.root;

        if (enemyAnimator == null)
            enemyAnimator = root.GetComponentInChildren<Animator>();

        if (enemyStatus == null)
            enemyStatus = root.GetComponent<EnemyStatus>();

        if (weaponAnimatorState == null)
            weaponAnimatorState = GetComponent<EnemyWeaponAnimatorState>();

        if (weaponAnimatorState == null)
            weaponAnimatorState = GetComponentInChildren<EnemyWeaponAnimatorState>(true);

        if (weaponAnimatorState == null)
            weaponAnimatorState = root.GetComponentInChildren<EnemyWeaponAnimatorState>(true);

        ResolveEnemyTotalAmmoSetter();
    }

    private void ResolveEnemyTotalAmmoSetter()
    {
        if (enemyTotalAmmoSetter != null)
            return;

        Transform root = transform.root;

        enemyTotalAmmoSetter = root.GetComponent<TotalAmmoSetter>();

        if (enemyTotalAmmoSetter == null)
            enemyTotalAmmoSetter = root.GetComponentInChildren<TotalAmmoSetter>();
    }

    public bool CanShoot()
    {
        if (!usesAmmoAndReload)
            return true;

        return !isReloading && currentAmmoInMagazine > 0;
    }

    public bool TryCancelReloadForShoot()
    {
        if (!usesAmmoAndReload)
            return true;

        if (!isReloading)
            return currentAmmoInMagazine > 0;

        if (!allowShootCancelOneByOneReload)
            return false;

        if (reloadMode != EnemyReloadMode.OneByOne)
            return false;

        if (currentAmmoInMagazine <= 0)
            return false;

        CancelReload();
        return true;
    }

    public bool TryConsumeOneRound()
    {
        if (!usesAmmoAndReload)
            return true;

        if (currentAmmoInMagazine <= 0)
            return false;

        currentAmmoInMagazine--;
        return true;
    }

    public bool CanReload()
    {
        if (!usesAmmoAndReload)
            return false;

        if (isReloading)
            return false;

        if (currentAmmoInMagazine >= magazineSize)
            return false;

        if (enemyTotalAmmoSetter == null)
            return false;

        if (!enemyTotalAmmoSetter.HasAmmo(ammoType))
            return false;

        return true;
    }

    public bool TryStartReload()
    {
        if (!usesAmmoAndReload)
            return false;

        if (!CanReload())
            return false;

        StopReloadRoutineOnly();

        if (reloadMode == EnemyReloadMode.OneByOne)
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
        UploadReloadingStatus();
        RestoreRigWeight();

        SetAnimatorReloadingBool(false);
        SetAnimatorKeepReloadingBool(false);
    }

    private IEnumerator MagazineReloadRoutine()
    {
        BeginReloadProcess(keepReloadingInAnimator: false);

        SetAnimatorReloadingBool(true);
        PlayReloadAnimation();

        yield return new WaitForSeconds(magazineReloadDuration);

        int need = Mathf.Max(0, magazineSize - currentAmmoInMagazine);
        int loaded = enemyTotalAmmoSetter != null ? enemyTotalAmmoSetter.ConsumeAmmo(ammoType, need) : 0;

        currentAmmoInMagazine += loaded;
        currentAmmoInMagazine = Mathf.Clamp(currentAmmoInMagazine, 0, magazineSize);

        EndReloadProcess();
        reloadRoutine = null;
    }

    private IEnumerator OneByOneReloadRoutine()
    {
        BeginReloadProcess(keepReloadingInAnimator: true);

        while (currentAmmoInMagazine < magazineSize &&
               enemyTotalAmmoSetter != null &&
               enemyTotalAmmoSetter.HasAmmo(ammoType))
        {
            SetAnimatorReloadingBool(true);
            PlayReloadAnimation();

            yield return new WaitForSeconds(oneByOneReloadDurationPerRound);

            int loaded = enemyTotalAmmoSetter.ConsumeAmmo(ammoType, 1);

            if (loaded <= 0)
                break;

            currentAmmoInMagazine += loaded;
            currentAmmoInMagazine = Mathf.Clamp(currentAmmoInMagazine, 0, magazineSize);

            SetAnimatorReloadingBool(false);

            if (currentAmmoInMagazine >= magazineSize)
                break;

            if (enemyTotalAmmoSetter == null || !enemyTotalAmmoSetter.HasAmmo(ammoType))
                break;

            if (oneByOneReloadBoolOffGap > 0f)
                yield return new WaitForSeconds(oneByOneReloadBoolOffGap);
            else
                yield return null;
        }

        EndReloadProcess();
        reloadRoutine = null;
    }

    private void BeginReloadProcess(bool keepReloadingInAnimator)
    {
        isReloading = true;
        UploadReloadingStatus();
        ApplyReloadRigWeight();
        ApplyWeaponAnimatorStateBeforeReload();

        SetAnimatorKeepReloadingBool(keepReloadingInAnimator);
    }

    private void ApplyWeaponAnimatorStateBeforeReload()
    {
        if (!applyWeaponAnimatorStateBeforeReload)
            return;

        if (weaponAnimatorState == null)
            ResolveReferences();

        if (weaponAnimatorState != null)
            weaponAnimatorState.ApplyWeaponAnimatorState();
    }

    private void EndReloadProcess()
    {
        isReloading = false;
        UploadReloadingStatus();
        RestoreRigWeight();

        SetAnimatorReloadingBool(false);
        SetAnimatorKeepReloadingBool(false);
    }

    private void UploadReloadingStatus()
    {
        if (enemyStatus == null)
            enemyStatus = transform.root.GetComponent<EnemyStatus>();

        if (enemyStatus != null)
            enemyStatus.SetReloading(IsReloading);
    }

    private void SetAnimatorReloadingBool(bool value)
    {
        if (enemyAnimator == null)
            return;

        if (string.IsNullOrWhiteSpace(isReloadingBoolName))
            return;

        enemyAnimator.SetBool(isReloadingBoolHash, value);
    }

    private void SetAnimatorKeepReloadingBool(bool value)
    {
        if (enemyAnimator == null)
            return;

        if (string.IsNullOrWhiteSpace(keepReloadingBoolName))
            return;

        enemyAnimator.SetBool(keepReloadingBoolHash, value);
    }

    private void PlayReloadAnimation()
    {
        if (enemyAnimator == null)
            return;

        if (!string.IsNullOrWhiteSpace(reloadTriggerName))
        {
            if (resetTriggerBeforeSet)
                enemyAnimator.ResetTrigger(reloadTriggerHash);

            enemyAnimator.SetTrigger(reloadTriggerHash);
        }

        if (!forceReloadStateByName)
            return;

        string stateName = reloadMode == EnemyReloadMode.OneByOne
            ? oneByOneReloadStateName
            : magazineReloadStateName;

        if (string.IsNullOrWhiteSpace(stateName))
            return;

        enemyAnimator.CrossFadeInFixedTime(
            stateName,
            reloadCrossFadeDuration,
            reloadLayerIndex
        );
    }

    private void StopReloadRoutineOnly()
    {
        if (reloadRoutine != null)
        {
            StopCoroutine(reloadRoutine);
            reloadRoutine = null;
        }
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
