using UnityEngine;

namespace Pickups
{
    public enum PickupType
    {
        Health,
        Ammo,
        Grenade
    }

    public class Pickup : MonoBehaviour
    {
        [Header("Pickup Settings")]
        [Tooltip("Type of pickup")]
        [SerializeField] private PickupType pickupType = PickupType.Health;

        [Tooltip("Amount to give (health points, ammo count, or grenade count)")]
        [SerializeField] private int amount = 10;

        [Tooltip("For grenade pickups, specify the grenade type")]
        [SerializeField] private PlayerGrenadeSlots.GrenadeType grenadeType = PlayerGrenadeSlots.GrenadeType.Frag;

        [Header("Effects")]
        [Tooltip("Optional pickup sound effect")]
        [SerializeField] private AudioClip pickupSound;

        [Tooltip("Optional particle effect to spawn on pickup")]
        [SerializeField] private GameObject pickupEffect;

        [Tooltip("Whether to destroy the pickup after being collected")]
        [SerializeField] private bool destroyOnPickup = true;

        private void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponentInParent<PlayerHealth>();
            if (player == null)
                return;

            ApplyPickup(player);

            if (pickupSound != null)
                AudioSource.PlayClipAtPoint(pickupSound, transform.position);

            if (pickupEffect != null)
                Instantiate(pickupEffect, transform.position, Quaternion.identity);

            if (destroyOnPickup)
                Destroy(gameObject);
        }

        private void ApplyPickup(PlayerHealth player)
        {
            switch (pickupType)
            {
                case PickupType.Health:
                    player.Heal(amount);
                    break;

                case PickupType.Ammo:
                    var weaponSlots = player.GetComponent<PlayerWeaponSlots>();
                    if (weaponSlots == null)
                        break;

                    var ammoSettings = weaponSlots.CurrentAmmoSettings;
                    if (ammoSettings != null)
                        ammoSettings.AddReserveAmmo(amount);
                    break;

                case PickupType.Grenade:
                    var grenadeSlots = player.GetComponent<PlayerGrenadeSlots>();
                    if (grenadeSlots == null)
                        break;

                    var slot = grenadeSlots.GetSlotByType(grenadeType);
                    if (slot != null && slot.count >= 0)
                        slot.count += amount;
                    break;
            }
        }
    }
}
