using UnityEngine;

public class EnemyVictoryChecker : MonoBehaviour
{
    public GameFlowManager gameFlowManager;
    public string enemyTag = "Enemy";
    public float checkDelay = 1f;

    private bool victoryTriggered = false;

    void Start()
    {
        InvokeRepeating(nameof(CheckEnemies), checkDelay, checkDelay);
    }

    void CheckEnemies()
    {
        if (victoryTriggered)
            return;

        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);

        if (enemies.Length == 0)
        {
            victoryTriggered = true;
            gameFlowManager.LoadVictory();
        }
    }
}