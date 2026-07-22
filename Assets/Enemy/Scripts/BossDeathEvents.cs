using UnityEngine;

[DisallowMultipleComponent]
public class BossDeathEvents : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyHealth enemyHealth;
    [SerializeField] private MusicManager musicManager;
    [SerializeField] private WaveClearDisplay waveClearDisplay;
    [SerializeField] private GameFlowManager gameFlowManager;

    [Header("Settings")]
    [SerializeField] private string bossDefeatedText = "BOSS DEFEATED";
    [SerializeField] private float victoryDelay = 15f;

    private void Awake()
    {
        if (enemyHealth      == null) enemyHealth      = GetComponent<EnemyHealth>();
        if (musicManager     == null) musicManager     = FindFirstObjectByType<MusicManager>();
        if (waveClearDisplay == null) waveClearDisplay = FindFirstObjectByType<WaveClearDisplay>();
        if (gameFlowManager  == null) gameFlowManager  = FindFirstObjectByType<GameFlowManager>();
    }

    private void OnEnable()
    {
        if (enemyHealth != null)
            enemyHealth.onDeath.AddListener(OnBossDied);
    }

    private void OnDisable()
    {
        if (enemyHealth != null)
            enemyHealth.onDeath.RemoveListener(OnBossDied);
    }

    private void OnBossDied()
    {
        if (waveClearDisplay != null)
            waveClearDisplay.ShowWithText(bossDefeatedText, OnBossDisplayFinished);

        if (gameFlowManager != null)
            gameFlowManager.LoadVictoryDelayed(victoryDelay);
    }

    private void OnBossDisplayFinished()
    {
        if (musicManager != null)
            musicManager.PlayBossDefeatedMusic();
    }
}
