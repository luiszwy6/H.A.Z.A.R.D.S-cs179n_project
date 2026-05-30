using System.Collections.Generic;
using UnityEngine;

namespace Pickups
{
    public class PickupDropper : MonoBehaviour
    {
        [System.Serializable]
        public class DropEntry
        {
            public GameObject pickupPrefab;
            [Range(0f, 1f)] public float dropChance = 0.5f;
        }

        [Header("Drop Table")]
        [SerializeField] private List<DropEntry> dropTable = new List<DropEntry>();

        [Header("Spawn Settings")]
        [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 0.5f, 0f);
        [SerializeField] private float randomSpread = 0.3f;

        public void TryDrop()
        {
            foreach (DropEntry entry in dropTable)
            {
                if (entry.pickupPrefab == null)
                    continue;

                if (Random.value <= entry.dropChance)
                    SpawnPickup(entry.pickupPrefab);
            }
        }

        private void SpawnPickup(GameObject prefab)
        {
            Vector3 spread = new Vector3(
                Random.Range(-randomSpread, randomSpread),
                0f,
                Random.Range(-randomSpread, randomSpread)
            );

            Instantiate(prefab, transform.position + spawnOffset + spread, Quaternion.identity);
        }
    }
}
