using UnityEngine;

[DisallowMultipleComponent]
public class EnemyShootLockController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyWeaponShooter weaponShooter;
    [SerializeField] private EnemyAnimatorParameterDriver animatorDriver;

    [Header("Lock Settings")]
    [SerializeField] private bool clearShootingOnLock = true;
    [SerializeField] private bool clearShootingWhileLocked = true;
    [SerializeField] private bool restoreShootLockOnUnlock = true;

    [Header("Debug")]
    [SerializeField] private int debugLockCount;
    [SerializeField] private bool debugIsShootLocked;

    private int lockCount;

    private bool cachedExternalShootLock;
    private bool hasCachedShootState;

    public int LockCount
    {
        get { return lockCount; }
    }

    public bool IsShootLocked
    {
        get { return lockCount > 0; }
    }

    private void Awake()
    {
        if (weaponShooter == null)
            weaponShooter = GetComponentInChildren<EnemyWeaponShooter>(true);

        if (animatorDriver == null)
            animatorDriver = GetComponent<EnemyAnimatorParameterDriver>();
    }

    private void LateUpdate()
    {
        UpdateDebugState();

        if (!IsShootLocked)
            return;

        ApplyLock();
    }

    public void AddShootLock()
    {
        lockCount++;
        UpdateDebugState();

        if (lockCount == 1)
        {
            CacheShootState();
            ApplyLock();
        }
    }

    public void RemoveShootLock()
    {
        lockCount = Mathf.Max(0, lockCount - 1);
        UpdateDebugState();

        if (lockCount == 0)
            RestoreShootState();
    }

    public void ClearShootLocks()
    {
        lockCount = 0;
        UpdateDebugState();
        RestoreShootState();
    }

    private void UpdateDebugState()
    {
        debugLockCount = lockCount;
        debugIsShootLocked = IsShootLocked;
    }

    private void ApplyLock()
    {
        if (weaponShooter != null)
            weaponShooter.externalShootLock = true;

        if (animatorDriver != null && clearShootingWhileLocked)
        {
            animatorDriver.SetShooting(false);
            animatorDriver.SetKeepShooting(false);
        }

        if (weaponShooter != null && clearShootingOnLock)
            weaponShooter.ForceClearRuntimeState();

        if (weaponShooter != null)
            weaponShooter.externalShootLock = true;
    }

    private void CacheShootState()
    {
        if (hasCachedShootState)
            return;

        if (weaponShooter != null)
            cachedExternalShootLock = weaponShooter.externalShootLock;

        hasCachedShootState = true;
    }

    private void RestoreShootState()
    {
        if (!hasCachedShootState)
            return;

        if (weaponShooter != null && restoreShootLockOnUnlock)
            weaponShooter.externalShootLock = cachedExternalShootLock;

        hasCachedShootState = false;
    }
}
