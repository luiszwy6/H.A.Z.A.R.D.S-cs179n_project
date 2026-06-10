using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ZombieGenerator : MonoBehaviour
{
    [System.Serializable]
    public class ZombieSpawnPoint
    {
        public Transform point;
        [Min(1)] public int countPerSpawn = 2;
    }

    [Header("References")]
    [SerializeField] private EnemySquadGenerator squadGenerator;

    [Header("Zombie Prefab")]
    [SerializeField] private GameObject zombiePrefab;

    [Header("Spawn Points")]
    [SerializeField] private List<ZombieSpawnPoint> spawnPoints = new List<ZombieSpawnPoint>();

    [Header("Timing")]
    [Min(0f)] [SerializeField] private float initialDelay = 5f;
    [Min(1f)] [SerializeField] private float spawnInterval = 30f;

    [Header("Limit")]
    [Min(0)] [SerializeField] private int maxTotalZombies = 0; // 0 = unlimited

    [Header("Spawn Area")]
    [Min(0f)] [SerializeField] private float spawnRadius = 1f;

    private bool isActive;
    private Coroutine spawnRoutine;
    private int totalSpawned;

    private void Awake()
    {
        if (squadGenerator == null)
            squadGenerator = FindFirstObjectByType<EnemySquadGenerator>();
    }

    private void OnEnable()
    {
        if (squadGenerator == null)
            return;

        squadGenerator.OnStartedGenerating += StartGenerating;
        squadGenerator.onAllClear.AddListener(StopGenerating);
    }

    private void OnDisable()
    {
        if (squadGenerator != null)
        {
            squadGenerator.OnStartedGenerating -= StartGenerating;
            squadGenerator.onAllClear.RemoveListener(StopGenerating);
        }

        StopGenerating();
    }

    private void StartGenerating()
    {
        if (isActive)
            return;

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
        if (zombiePrefab == null)
            return;

        foreach (ZombieSpawnPoint sp in spawnPoints)
        {
            if (sp.point == null)
                continue;

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
