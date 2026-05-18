using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class ResourcePickup : MonoBehaviour
{
    public enum PickupType
    {
        Health,
        Ammo,
        Grenade
    }

    public enum AmmoTargetMode
    {
        CurrentWeapon,
        OverrideType
    }

    public enum GrenadeTargetMode
    {
        CurrentSlot,
        SpecificType
    }

    [Header("Pickup Type")]
    [SerializeField] private PickupType pickupType = PickupType.Health;

    [Header("Health")]
    [Min(1f)] [SerializeField] private float healthAmount = 25f;

    [Header("Ammo")]
    [Min(1)] [SerializeField] private int ammoAmount = 30;
    [SerializeField] private AmmoTargetMode ammoTargetMode = AmmoTargetMode.CurrentWeapon;
    [SerializeField] private TotalAmmoSetter.AmmoType ammoTypeOverride = TotalAmmoSetter.AmmoType.AssaultRifle;
    [SerializeField] private bool fallbackToOverrideIfNoCurrentWeapon = true;

    [Header("Grenades")]
    [Min(1)] [SerializeField] private int grenadeAmount = 1;
    [SerializeField] private GrenadeTargetMode grenadeTargetMode = GrenadeTargetMode.CurrentSlot;
    [SerializeField] private PlayerGrenadeSlots.GrenadeType grenadeTypeOverride = PlayerGrenadeSlots.GrenadeType.Frag;

    [Header("Pickup Rules")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool requirePlayerTag = true;
    [SerializeField] private bool destroyOnPickup = true;

    private bool pickedUp;

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (pickedUp)
            return;

        if (other == null)
            return;

        Transform root = other.transform.root;
        if (root == null)
            return;

        if (requirePlayerTag && !root.CompareTag(playerTag))
            return;

        PlayerHealth playerHealth = root.GetComponent<PlayerHealth>();
        PlayerWeaponSlots playerWeaponSlots = root.GetComponent<PlayerWeaponSlots>();
        PlayerGrenadeSlots playerGrenadeSlots = root.GetComponent<PlayerGrenadeSlots>();

        TotalAmmoSetter ammoPool = root.GetComponent<TotalAmmoSetter>();
        if (ammoPool == null)
            ammoPool = root.GetComponentInChildren<TotalAmmoSetter>();

        bool applied = TryApplyPickup(playerHealth, playerWeaponSlots, playerGrenadeSlots, ammoPool);
        if (!applied)
            return;

        pickedUp = true;

        if (destroyOnPickup)
            Destroy(gameObject);
    }

    private bool TryApplyPickup(
        PlayerHealth playerHealth,
        PlayerWeaponSlots playerWeaponSlots,
        PlayerGrenadeSlots playerGrenadeSlots,
        TotalAmmoSetter ammoPool)
    {
        switch (pickupType)
        {
            case PickupType.Health:
                return TryApplyHealth(playerHealth);

            case PickupType.Ammo:
                return TryApplyAmmo(playerWeaponSlots, ammoPool);

            case PickupType.Grenade:
                return TryApplyGrenades(playerGrenadeSlots);
        }

        return false;
    }

    private bool TryApplyHealth(PlayerHealth playerHealth)
    {
        if (playerHealth == null)
            return false;

        if (healthAmount <= 0f)
            return false;

        playerHealth.Heal(healthAmount);
        return true;
    }

    private bool TryApplyAmmo(PlayerWeaponSlots playerWeaponSlots, TotalAmmoSetter ammoPool)
    {
        if (ammoAmount <= 0)
            return false;

        if (ammoPool == null)
            ammoPool = FindFirstObjectByType<TotalAmmoSetter>();

        if (ammoPool == null)
            return false;

        TotalAmmoSetter.AmmoType ammoType = ammoTypeOverride;

        if (ammoTargetMode == AmmoTargetMode.CurrentWeapon)
        {
            if (playerWeaponSlots != null && playerWeaponSlots.CurrentAmmoSettings != null)
            {
                ammoType = playerWeaponSlots.CurrentAmmoSettings.AmmoType;
            }
            else if (!fallbackToOverrideIfNoCurrentWeapon)
            {
                return false;
            }
        }

        ammoPool.AddAmmo(ammoType, ammoAmount);
        return true;
    }

    private bool TryApplyGrenades(PlayerGrenadeSlots playerGrenadeSlots)
    {
        if (playerGrenadeSlots == null)
            return false;

        if (grenadeAmount <= 0)
            return false;

        PlayerGrenadeSlots.GrenadeType grenadeType = grenadeTypeOverride;

        if (grenadeTargetMode == GrenadeTargetMode.CurrentSlot)
        {
            if (playerGrenadeSlots.CurrentGrenadeIndex >= 0)
                grenadeType = playerGrenadeSlots.CurrentGrenadeType;
        }

        return playerGrenadeSlots.AddGrenades(grenadeType, grenadeAmount, true);
    }
}
