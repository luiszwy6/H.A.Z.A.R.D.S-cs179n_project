using UnityEngine;

[DisallowMultipleComponent]
public class EnemyWeaponAnimatorState : MonoBehaviour
{
    public enum EnemyWeaponType
    {
        AssaultRifle,
        ShotGun,
        Sniper,
        Pistol,
        ShieldAxe
    }

    [Header("Weapon Type")]
    [SerializeField] private EnemyWeaponType weaponType = EnemyWeaponType.AssaultRifle;

    [Header("Animator")]
    [SerializeField] private Animator enemyAnimator;
    [SerializeField] private bool applyOnAwake = true;
    [SerializeField] private bool applyOnEnable = true;
    [SerializeField] private bool clearOtherWeaponBools = true;

    [Header("Animator Weapon Bool Names")]
    [SerializeField] private string assaultRifleBoolName = "AssaultRifle";
    [SerializeField] private string shotGunBoolName = "ShotGun";
    [SerializeField] private string sniperBoolName = "Sniper";
    [SerializeField] private string pistolBoolName = "Pistol";
    [SerializeField] private string shieldAxeBoolName = "ShieldAxe";
    private int shieldAxeBoolHash;

    private int assaultRifleBoolHash;
    private int shotGunBoolHash;
    private int sniperBoolHash;
    private int pistolBoolHash;

    private void Awake()
    {
        ResolveReferences();
        CacheHashes();

        if (applyOnAwake)
            ApplyWeaponAnimatorState();
    }

    private void OnEnable()
    {
        ResolveReferences();
        CacheHashes();

        if (applyOnEnable)
            ApplyWeaponAnimatorState();
    }

    private void ResolveReferences()
    {
        if (enemyAnimator != null)
            return;

        Transform root = transform.root;
        enemyAnimator = root.GetComponentInChildren<Animator>();
    }

    private void CacheHashes()
    {
        assaultRifleBoolHash = Animator.StringToHash(assaultRifleBoolName);
        shotGunBoolHash = Animator.StringToHash(shotGunBoolName);
        sniperBoolHash = Animator.StringToHash(sniperBoolName);
        pistolBoolHash = Animator.StringToHash(pistolBoolName);
        shieldAxeBoolHash = Animator.StringToHash(shieldAxeBoolName);
    }

    public void ApplyWeaponAnimatorState()
    {
        if (enemyAnimator == null)
            return;

        if (clearOtherWeaponBools)
            ClearAllWeaponBools();

        switch (weaponType)
        {
            case EnemyWeaponType.AssaultRifle:
                SetBoolSafe(assaultRifleBoolHash, assaultRifleBoolName, true);
                break;

            case EnemyWeaponType.ShotGun:
                SetBoolSafe(shotGunBoolHash, shotGunBoolName, true);
                break;

            case EnemyWeaponType.Sniper:
                SetBoolSafe(sniperBoolHash, sniperBoolName, true);
                break;

            case EnemyWeaponType.Pistol:
                SetBoolSafe(pistolBoolHash, pistolBoolName, true);
                break;

            case EnemyWeaponType.ShieldAxe:
            SetBoolSafe(shieldAxeBoolHash, shieldAxeBoolName, true);
            break;
        }
    }

    public void ClearAllWeaponBools()
    {
        if (enemyAnimator == null)
            return;

        SetBoolSafe(assaultRifleBoolHash, assaultRifleBoolName, false);
        SetBoolSafe(shotGunBoolHash, shotGunBoolName, false);
        SetBoolSafe(sniperBoolHash, sniperBoolName, false);
        SetBoolSafe(pistolBoolHash, pistolBoolName, false);
        SetBoolSafe(shieldAxeBoolHash, shieldAxeBoolName, false);
    }

    private void SetBoolSafe(int hash, string parameterName, bool value)
    {
        if (enemyAnimator == null)
            return;

        if (string.IsNullOrWhiteSpace(parameterName))
            return;

        enemyAnimator.SetBool(hash, value);
    }
}