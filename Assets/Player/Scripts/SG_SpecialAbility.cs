using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class SG_SpecialAbility : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private KillCount killCount;
    [SerializeField] private PlayerWeaponSlots weaponSlots;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerInput playerInput;

    [Header("Ability Settings")]
    [Min(0.1f)] [SerializeField] private float abilityDuration = 10f;
    [SerializeField] private string abilityActionName = "SpecialAbility";

    [Header("Armor Override")]
    [Range(0, 2)] [SerializeField] private int abilityArmorLevel = 2;

    [Header("Reload Override")]
    [Tooltip("Shells loaded per one-by-one reload cycle during ability.")]
    [Min(1)] [SerializeField] private int shellsPerReload = 4;

    [Header("Debug")]
    [SerializeField] private bool isActive;
    [SerializeField] private float remainingTime;

    public bool IsActive => isActive;
    public float RemainingTime => remainingTime;
    public float Duration => abilityDuration;

    private InputAction abilityAction;
    private WeaponAmmoSettings sgAmmoSettings;
    private int savedArmorLevel;

    private void Awake()
    {
        Transform root = transform.root;

        if (killCount == null)
            killCount = root.GetComponentInChildren<KillCount>();

        if (weaponSlots == null)
            weaponSlots = root.GetComponent<PlayerWeaponSlots>();

        if (playerHealth == null)
            playerHealth = root.GetComponent<PlayerHealth>();

        if (playerInput == null)
            playerInput = root.GetComponent<PlayerInput>();
    }

    private void OnEnable()
    {
        if (playerInput != null && playerInput.actions != null)
            abilityAction = playerInput.actions.FindAction(abilityActionName, false);
    }

    private void OnDisable()
    {
        if (isActive)
            EndAbility();
    }

    private void Update()
    {
        if (abilityAction != null && abilityAction.WasPressedThisFrame())
            TryActivate();

        if (isActive)
        {
            remainingTime -= Time.deltaTime;

            if (remainingTime <= 0f)
                EndAbility();
        }
    }

    public bool TryActivate()
    {
        if (isActive)
            return false;

        if (killCount == null || !killCount.IsCharged)
            return false;

        WeaponAmmoSettings ammo = ResolveAmmoSettings();

        if (ammo == null)
            return false;

        Activate(ammo);
        return true;
    }

    private void Activate(WeaponAmmoSettings ammo)
    {
        isActive = true;
        remainingTime = abilityDuration;
        sgAmmoSettings = ammo;

        savedArmorLevel = playerHealth != null ? playerHealth.CurrentArmorLevel : 0;

        if (playerHealth != null)
            playerHealth.SetArmorLevel(abilityArmorLevel);

        sgAmmoSettings.SetOneByOneLoadPerRound(shellsPerReload);
        sgAmmoSettings.allowRunWhileReloading = true;

        if (weaponSlots != null)
            weaponSlots.SetExternalSwitchLock(true);
    }

    private void EndAbility()
    {
        isActive = false;
        remainingTime = 0f;

        if (playerHealth != null)
            playerHealth.SetArmorLevel(savedArmorLevel);

        if (sgAmmoSettings != null)
        {
            sgAmmoSettings.SetOneByOneLoadPerRound(1);
            sgAmmoSettings.allowRunWhileReloading = false;
        }

        if (weaponSlots != null)
            weaponSlots.SetExternalSwitchLock(false);

        sgAmmoSettings = null;
        killCount?.ResetKills();
    }

    private WeaponAmmoSettings ResolveAmmoSettings()
    {
        WeaponAmmoSettings self = GetComponentInChildren<WeaponAmmoSettings>(true);

        if (self != null)
        {
            if (!gameObject.activeInHierarchy)
                return null;

            if (self.AmmoType != TotalAmmoSetter.AmmoType.Shotgun)
                return null;

            return self;
        }

        if (weaponSlots == null)
            return null;

        WeaponAmmoSettings current = weaponSlots.CurrentAmmoSettings;

        if (current != null && current.AmmoType == TotalAmmoSetter.AmmoType.Shotgun)
            return current;

        return null;
    }
}
