using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PickupSpawnGenerator : MonoBehaviour
{
    [System.Serializable]
    public class PickupSpawnEntry
    {
        public GameObject prefab;
        public int count = 1;
        public Vector3 minLocalBounds = new Vector3(-5f, 0f, -5f);
        public Vector3 maxLocalBounds = new Vector3(5f, 0f, 5f);
        public float yOffset = 0f;
    }

    [Header("Spawn Setup")]
    [SerializeField] private List<PickupSpawnEntry> entries = new List<PickupSpawnEntry>();
    [SerializeField] private bool spawnOnStart = false;
    [SerializeField] private bool clearPreviousChildrenOnSpawn = true;
    [SerializeField] private bool randomYaw = true;

    private void Start()
    {
        if (spawnOnStart)
            Spawn();
    }

    [ContextMenu("Spawn Pickups")]
    public void Spawn()
    {
        if (clearPreviousChildrenOnSpawn)
            ClearChildren();

        if (entries == null || entries.Count == 0)
            return;

        for (int i = 0; i < entries.Count; i++)
        {
            PickupSpawnEntry entry = entries[i];
            if (entry == null || entry.prefab == null)
                continue;

            int spawnCount = Mathf.Max(0, entry.count);
            for (int j = 0; j < spawnCount; j++)
                SpawnOne(entry);
        }
    }

    private void SpawnOne(PickupSpawnEntry entry)
    {
        Vector3 localPos = new Vector3(
            Random.Range(entry.minLocalBounds.x, entry.maxLocalBounds.x),
            Random.Range(entry.minLocalBounds.y, entry.maxLocalBounds.y) + entry.yOffset,
            Random.Range(entry.minLocalBounds.z, entry.maxLocalBounds.z)
        );

        Quaternion rotation = randomYaw
            ? Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)
            : Quaternion.identity;

        Instantiate(entry.prefab, transform.TransformPoint(localPos), rotation, transform);
    }

    private void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child == null)
                continue;

            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }
    }
}
