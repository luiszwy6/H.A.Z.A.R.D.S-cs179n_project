using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class AR_SpecialAbility : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private KillCount killCount;
    [SerializeField] private PlayerWeaponSlots weaponSlots;
    [SerializeField] private TotalAmmoSetter totalAmmoSetter;
    [SerializeField] private PlayerInput playerInput;

    [Header("Ability Settings")]
    [Min(0.1f)] [SerializeField] private float abilityDuration = 8f;
    [SerializeField] private string abilityActionName = "SpecialAbility";

    [Header("End State")]
    [Tooltip("Bullets remaining in magazine when ability ends. -1 = keep whatever is in there.")]
    [SerializeField] private int magazineAmmoOnEnd = 0;

    [Header("Debug")]
    [SerializeField] private bool isActive;
    [SerializeField] private float remainingTime;
    [SerializeField] private bool debugLog = false;

    public bool IsActive => isActive;
    public float RemainingTime => remainingTime;
    public float Duration => abilityDuration;

    private InputAction abilityAction;
    private WeaponAmmoSettings arAmmoSettings;
    private int savedTotalAmmoOnActivate;

    private void Awake()
    {
        Transform root = transform.root;

        if (killCount == null)
            killCount = root.GetComponentInChildren<KillCount>();

        if (weaponSlots == null)
            weaponSlots = root.GetComponent<PlayerWeaponSlots>();

        if (totalAmmoSetter == null)
            totalAmmoSetter = FindFirstObjectByType<TotalAmmoSetter>();

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
        {
            if (debugLog)
                Debug.Log(
                    $"[AR_SpecialAbility] Button pressed. action.enabled={abilityAction.enabled}" +
                    $"  charged={killCount?.IsCharged}  ammo={ResolveAmmoSettings()?.name ?? "NULL"}",
                    this
                );

            TryActivate();
        }

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
        {
            if (debugLog) Debug.Log("[AR_SpecialAbility] Already active.", this);
            return false;
        }

        if (killCount == null)
        {
            if (debugLog) Debug.LogWarning("[AR_SpecialAbility] KillCount is null.", this);
            return false;
        }

        if (!killCount.IsCharged)
        {
            if (debugLog) Debug.Log($"[AR_SpecialAbility] Not charged: {killCount.CurrentKills}/{killCount.RequiredKills}.", this);
            return false;
        }

        WeaponAmmoSettings ammo = ResolveAmmoSettings();

        if (ammo == null)
        {
            if (debugLog) Debug.LogWarning("[AR_SpecialAbility] ResolveAmmoSettings returned null.", this);
            return false;
        }

        Activate(ammo);
        return true;
    }

    private void Activate(WeaponAmmoSettings ammo)
    {
        isActive = true;
        remainingTime = abilityDuration;
        arAmmoSettings = ammo;

        savedTotalAmmoOnActivate = totalAmmoSetter != null
            ? totalAmmoSetter.GetAmmoCount(TotalAmmoSetter.AmmoType.AssaultRifle)
            : 0;

        arAmmoSettings.SetInfiniteAmmoMode(true);

        if (weaponSlots != null)
            weaponSlots.SetExternalSwitchLock(true);
    }

    private void EndAbility()
    {
        isActive = false;
        remainingTime = 0f;

        if (weaponSlots != null)
            weaponSlots.SetExternalSwitchLock(false);

        if (arAmmoSettings != null)
        {
            arAmmoSettings.SetInfiniteAmmoMode(false);
            ApplyEndAmmoState();
        }

        arAmmoSettings = null;
        killCount?.ResetKills();
    }

    private void ApplyEndAmmoState()
    {
        if (magazineAmmoOnEnd < 0)
            return;

        int clampedMag = Mathf.Clamp(magazineAmmoOnEnd, 0, arAmmoSettings.MagazineSize);
        arAmmoSettings.ForceSetCurrentAmmo(clampedMag);

        if (totalAmmoSetter == null)
            return;

        int newTotal = Mathf.Max(0, savedTotalAmmoOnActivate - clampedMag);
        int currentTotal = totalAmmoSetter.GetAmmoCount(TotalAmmoSetter.AmmoType.AssaultRifle);
        int diff = newTotal - currentTotal;

        if (diff > 0)
            totalAmmoSetter.AddAmmo(TotalAmmoSetter.AmmoType.AssaultRifle, diff);
        else if (diff < 0)
            totalAmmoSetter.ConsumeAmmo(TotalAmmoSetter.AmmoType.AssaultRifle, -diff);
    }

    private WeaponAmmoSettings ResolveAmmoSettings()
    {
        WeaponAmmoSettings self = GetComponentInChildren<WeaponAmmoSettings>(true);

        if (debugLog) Debug.Log($"[AR_SpecialAbility] GetComponentInChildren<WeaponAmmoSettings>={self}, activeInHierarchy={gameObject.activeInHierarchy}, weaponSlots={weaponSlots}", this);

        if (self != null)
        {
            if (!gameObject.activeInHierarchy)
            {
                if (debugLog) Debug.Log("[AR_SpecialAbility] Weapon is inactive.", this);
                return null;
            }

            if (self.AmmoType != TotalAmmoSetter.AmmoType.AssaultRifle)
            {
                if (debugLog) Debug.Log($"[AR_SpecialAbility] AmmoType mismatch: found {self.AmmoType}, expected AssaultRifle.", this);
                return null;
            }

            return self;
        }

        if (weaponSlots == null)
        {
            if (debugLog) Debug.LogWarning("[AR_SpecialAbility] weaponSlots is null.", this);
            return null;
        }

        WeaponAmmoSettings current = weaponSlots.CurrentAmmoSettings;

        if (debugLog) Debug.Log($"[AR_SpecialAbility] weaponSlots.CurrentAmmoSettings={current}, type={current?.AmmoType}", this);

        if (current != null && current.AmmoType == TotalAmmoSetter.AmmoType.AssaultRifle)
            return current;

        return null;
    }
}
