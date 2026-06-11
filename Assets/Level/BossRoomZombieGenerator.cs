using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Like ZombieGenerator but starts spawning only after the BossStageDoor is opened.
/// Attach alongside a BossStageDoor or anywhere in the scene.
/// </summary>
public class BossRoomZombieGenerator : MonoBehaviour
{
    [System.Serializable]
    public class ZombieSpawnPoint
    {
        public Transform point;
        [Min(1)] public int countPerSpawn = 2;
    }

    [Header("References")]
    [Tooltip("Auto-found in scene if left empty.")]
    [SerializeField] private BossStageDoor door;

    [Header("Zombie Prefab")]
    [SerializeField] private GameObject zombiePrefab;

    [Header("Spawn Points")]
    [SerializeField] private List<ZombieSpawnPoint> spawnPoints = new List<ZombieSpawnPoint>();

    [Header("Timing")]
    [Min(0f)] [SerializeField] private float initialDelay = 3f;
    [Min(1f)] [SerializeField] private float spawnInterval = 20f;

    [Header("Limit")]
    [Tooltip("0 = unlimited.")]
    [Min(0)] [SerializeField] private int maxTotalZombies = 0;

    [Header("Spawn Area")]
    [Min(0f)] [SerializeField] private float spawnRadius = 1f;

    private bool isActive;
    private Coroutine spawnRoutine;
    private int totalSpawned;

    private void Awake()
    {
        if (door == null)
            door = FindFirstObjectByType<BossStageDoor>();
    }

    private void OnEnable()
    {
        if (door != null)
            door.OnDoorOpened += StartGenerating;
    }

    private void OnDisable()
    {
        if (door != null)
            door.OnDoorOpened -= StartGenerating;

        StopGenerating();
    }

    private void StartGenerating()
    {
        if (isActive) return;
        isActive = true;
        totalSpawned = 0;

        if (spawnRoutine != null)
            StopCoroutine(spawnRoutine);

        spawnRoutine = StartCoroutine(SpawnRoutine());
    }

    private void StopGenerating()
    {
        isActive = false;

        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }
    }

    private IEnumerator SpawnRoutine()
    {
        if (initialDelay > 0f)
            yield return new WaitForSeconds(initialDelay);

        while (isActive)
        {
            SpawnWave();
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    private void SpawnWave()
    {
        if (zombiePrefab == null) return;

        foreach (ZombieSpawnPoint sp in spawnPoints)
        {
            if (sp.point == null) continue;

            for (int i = 0; i < sp.countPerSpawn; i++)
            {
                if (maxTotalZombies > 0 && totalSpawned >= maxTotalZombies)
                {
                    StopGenerating();
                    return;
                }

                Vector2 circle = Random.insideUnitCircle * spawnRadius;
                Vector3 offset = new Vector3(circle.x, 0f, circle.y);

                Instantiate(zombiePrefab, sp.point.position + offset, Quaternion.identity);
                totalSpawned++;
            }
        }
    }
}
