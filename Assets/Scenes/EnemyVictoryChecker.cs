using UnityEngine;

[DisallowMultipleComponent]
public class EnemyVictoryChecker : MonoBehaviour
{
    [SerializeField] private GameFlowManager gameFlowManager;
    [SerializeField] private float checkInterval = 0.5f;

    private bool hasSeenEnemy;
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
        EnemyHealth[] enemies = FindObjectsOfType<EnemyHealth>(false);
        int livingEnemies = 0;

        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyHealth enemy = enemies[i];

            if (enemy == null || !enemy.gameObject.activeInHierarchy)
                continue;

            hasSeenEnemy = true;

            if (!enemy.IsDead)
                livingEnemies++;
        }

        if (!hasSeenEnemy || livingEnemies > 0)
            return;

        if (gameFlowManager == null)
            gameFlowManager = FindObjectOfType<GameFlowManager>();

        if (gameFlowManager != null)
            gameFlowManager.LoadVictory();
    }
}
