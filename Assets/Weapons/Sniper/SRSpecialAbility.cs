using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class SRSpecialAbility : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private KillCount         killCount;
    [SerializeField] private PlayerWeaponSlots weaponSlots;
    [SerializeField] private PlayerInput       playerInput;
    [SerializeField] private BulletTimeAbility srBulletTime;

    [Header("Ability Settings")]
    [Min(0.1f)] [SerializeField] private float abilityDuration        = 6f;
    [SerializeField]             private string abilityActionName      = "SpecialAbility";
    [SerializeField]             private float shootCooldownMultiplier = 0.4f;
    [SerializeField]             private float reloadSpeedMultiplier   = 2.5f;

    [Header("Debug")]
    [SerializeField] private bool  isActiveDebug;
    [SerializeField] private float remainingTimeDebug;

    public bool  IsActive      => isActiveDebug;
    public float RemainingTime => remainingTimeDebug;
    public float Duration      => abilityDuration;

    private InputAction       abilityAction;
    private SRShootSettings   shootSettings;
    private WeaponAmmoSettings ammoSettings;

    private void Awake()
    {
        Transform root = transform.root;

        if (killCount    == null) killCount    = root.GetComponentInChildren<KillCount>();
        if (weaponSlots  == null) weaponSlots  = root.GetComponent<PlayerWeaponSlots>();
        if (playerInput  == null) playerInput  = root.GetComponent<PlayerInput>();
        if (srBulletTime == null) srBulletTime = GetComponent<BulletTimeAbility>();
        if (shootSettings == null) shootSettings = GetComponent<SRShootSettings>();
        if (ammoSettings  == null) ammoSettings  = GetComponent<WeaponAmmoSettings>();
    }

    private void OnEnable()
    {
        if (playerInput != null && playerInput.actions != null)
            abilityAction = playerInput.actions.FindAction(abilityActionName, false);
    }

    private void OnDisable()
    {
        if (isActiveDebug) EndAbility();
    }

    private void Update()
    {
        if (abilityAction != null && abilityAction.WasPressedThisFrame())
            TryActivate();

        if (isActiveDebug)
        {
            remainingTimeDebug -= Time.unscaledDeltaTime;

            if (remainingTimeDebug <= 0f)
                EndAbility();
        }
    }

    public bool TryActivate()
    {
        if (isActiveDebug)                          return false;
        if (killCount == null || !killCount.IsCharged) return false;

        Activate();
        return true;
    }

    private void Activate()
    {
        isActiveDebug      = true;
        remainingTimeDebug = abilityDuration;

        srBulletTime?.SetExternalOverride(true);
        if (shootSettings != null) shootSettings.shootCooldownMultiplier = shootCooldownMultiplier;
        if (ammoSettings  != null) ammoSettings.reloadSpeedMultiplier    = reloadSpeedMultiplier;
        if (weaponSlots   != null) weaponSlots.SetExternalSwitchLock(true);
    }

    private void EndAbility()
    {
        isActiveDebug      = false;
        remainingTimeDebug = 0f;

        srBulletTime?.SetExternalOverride(false);
        if (shootSettings != null) shootSettings.shootCooldownMultiplier = 1f;
        if (ammoSettings  != null) ammoSettings.reloadSpeedMultiplier    = 1f;
        if (weaponSlots   != null) weaponSlots.SetExternalSwitchLock(false);

        killCount?.ResetKills();
    }
}
