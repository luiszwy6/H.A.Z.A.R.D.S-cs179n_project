using UnityEngine;

[DisallowMultipleComponent]
public class EnemyVictoryChecker : MonoBehaviour
{
    [SerializeField] private GameFlowManager gameFlowManager;
    [SerializeField] private float checkInterval = 0.5f;
    [SerializeField] private bool includeInactiveSceneEnemies = true;

    private bool hasSeenNonZombieEnemy;
    private float nextCheckTime;

    private void Awake()
    {
        if (gameFlowManager == null)
            gameFlowManager = FindObjectOfType<GameFlowManager>();
    }

    private void OnEnable()
    {
        EnemyHealth.OnAnyEnemyDied += HandleEnemyDied;
        nextCheckTime = 0f;
    }

    private void OnDisable()
    {
        EnemyHealth.OnAnyEnemyDied -= HandleEnemyDied;
    }

    private void Update()
    {
        if (Time.time < nextCheckTime)
            return;

        nextCheckTime = Time.time + Mathf.Max(0.1f, checkInterval);
        CheckForVictory();
    }

    private void HandleEnemyDied(EnemyHealth _)
    {
        CheckForVictory();
    }

    private void CheckForVictory()
    {
        EnemyHealth[] enemies = FindObjectsOfType<EnemyHealth>(includeInactiveSceneEnemies);
        int livingEnemies = 0;

        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyHealth enemy = enemies[i];

            if (enemy == null)
                continue;

            if (ShouldIgnoreForVictory(enemy))
                continue;

            hasSeenNonZombieEnemy = true;

            if (!enemy.IsDead)
                livingEnemies++;
        }

        if (!hasSeenNonZombieEnemy || livingEnemies > 0)
            return;

        if (gameFlowManager == null)
            gameFlowManager = FindObjectOfType<GameFlowManager>();

        if (gameFlowManager != null)
            gameFlowManager.LoadVictory();
    }

    private bool ShouldIgnoreForVictory(EnemyHealth enemy)
    {
        if (enemy == null)
            return true;

        Transform current = enemy.transform;

        while (current != null)
        {
            if (current.GetComponent<ZombieStatus>() != null)
                return true;

            string objectName = current.gameObject.name;

            if (objectName.Contains("EnemyPrototypes") ||
                objectName.Contains("ItemPrototypes"))
            {
                return true;
            }

            if (objectName.ToLowerInvariant().Contains("zombie"))
                return true;

            current = current.parent;
        }

        return false;
    }
}
