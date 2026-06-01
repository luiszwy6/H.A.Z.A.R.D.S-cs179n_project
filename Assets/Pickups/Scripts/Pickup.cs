using System.Collections.Generic;
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
        [System.Serializable]
        public class AmmoPickupEntry
        {
            public TotalAmmoSetter.AmmoType ammoType = TotalAmmoSetter.AmmoType.AssaultRifle;
            [Min(0)] public int amount = 30;
        }

        [Header("Pickup Settings")]
        [Tooltip("Type of pickup")]
        [SerializeField] private PickupType pickupType = PickupType.Health;

        [Tooltip("Health points to restore (Health pickups only)")]
        [SerializeField] private int amount = 10;

        [Tooltip("Ammo to add per type (Ammo pickups only)")]
        [SerializeField] private List<AmmoPickupEntry> ammoEntries = new List<AmmoPickupEntry>();

        [Tooltip("For grenade pickups, specify the grenade type")]
        [SerializeField] private PlayerGrenadeSlots.GrenadeType grenadeType = PlayerGrenadeSlots.GrenadeType.Frag;

        [Header("Effects")]
        [SerializeField] private AudioClip pickupSound;
        [Range(0f, 1f)] [SerializeField] private float pickupSoundVolume = 1f;
        [Tooltip("If assigned, plays through this AudioSource (2D). Otherwise plays at world position (3D).")]
        [SerializeField] private AudioSource pickupAudioSource;

        [SerializeField] private GameObject pickupEffect;
        [SerializeField] private bool destroyOnPickup = true;

        private void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponentInParent<PlayerHealth>();
            if (player == null)
                return;

            ApplyPickup(player);

            if (pickupSound != null)
            {
                if (pickupAudioSource != null)
                    pickupAudioSource.PlayOneShot(pickupSound, pickupSoundVolume);
                else
                    AudioSource.PlayClipAtPoint(pickupSound, transform.position, pickupSoundVolume);
            }

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
                    if (ammoEntries == null || ammoEntries.Count == 0)
                        break;

                    var totalAmmo = player.GetComponentInChildren<TotalAmmoSetter>();
                    if (totalAmmo == null)
                        break;

                    foreach (AmmoPickupEntry entry in ammoEntries)
                        totalAmmo.AddAmmo(entry.ammoType, entry.amount);
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
