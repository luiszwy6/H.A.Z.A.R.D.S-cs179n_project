using TMPro;
using UnityEngine;

public class SquadWaveHUD : MonoBehaviour
{
    [Header("Generator")]
    [SerializeField] private EnemySquadGenerator generator;

    [Header("Status")]
    [SerializeField] private TMP_Text statusText;

    [Header("Wave Info (hidden during Infiltrate)")]
    [SerializeField] private GameObject waveInfoPanel;
    [SerializeField] private TMP_Text waveText;
    [SerializeField] private TMP_Text enemyCountText;
    [SerializeField] private TMP_Text nextWaveText;

    [Header("Colors")]
    [SerializeField] private Color infiltrateColor = Color.white;
    [SerializeField] private Color alertColor      = Color.red;

    [Header("Labels")]
    [SerializeField] private string infiltrateLabel = "INFILTRATE";
    [SerializeField] private string alertLabel      = "ALERT";

    private bool  isAlert;
    private bool  isCountingDown;
    private float countdownRemaining;

    private void OnEnable()
    {
        if (generator == null) return;

        generator.OnStartedGenerating        += HandleStarted;
        generator.OnWaveStarted              += HandleWaveStarted;
        generator.OnWaveCleared              += HandleWaveCleared;
        generator.OnWaveSpawned              += HandleWaveSpawned;
        generator.onAllDiedThisRound.AddListener(HandleAllCleared);
        EnemyHealth.OnAnyEnemyDied           += HandleEnemyDied;
    }

    private void OnDisable()
    {
        if (generator == null) return;

        generator.OnStartedGenerating        -= HandleStarted;
        generator.OnWaveStarted              -= HandleWaveStarted;
        generator.OnWaveCleared              -= HandleWaveCleared;
        generator.OnWaveSpawned              -= HandleWaveSpawned;
        generator.onAllDiedThisRound.RemoveListener(HandleAllCleared);
        EnemyHealth.OnAnyEnemyDied           -= HandleEnemyDied;
    }

    private void Start()
    {
        if (generator != null && generator.IsStartingGenerate)
            ShowAlert();
        else
            ShowInfiltrate();
    }

    private void Update()
    {
        if (!isCountingDown)
            return;

        countdownRemaining -= Time.unscaledDeltaTime;

        if (countdownRemaining < 0f)
            countdownRemaining = 0f;

        if (nextWaveText != null)
            nextWaveText.text = $"Next wave in {countdownRemaining:F1}s";
    }

    // ── Handlers ─────────────────────────────────────────────────

    private void HandleStarted()
    {
        isAlert = true;
        ShowAlert();
    }

    private void HandleWaveStarted(int waveNum)
    {
        if (isAlert) RefreshWaveText();
    }

    private void HandleWaveCleared(int waveNum, float nextWaveDelay)
    {
        if (!isAlert) return;

        if (nextWaveDelay > 0f)
        {
            isCountingDown     = true;
            countdownRemaining = nextWaveDelay;

            if (nextWaveText != null)
            {
                nextWaveText.text = $"Next wave in {countdownRemaining:F1}s";
                nextWaveText.gameObject.SetActive(true);
            }

            if (enemyCountText != null)
                enemyCountText.gameObject.SetActive(false);
        }
    }

    private void HandleWaveSpawned(int waveNum)
    {
        StopCountdown();
        if (isAlert) RefreshEnemyCount();
    }

    private void HandleAllCleared()
    {
        StopCountdown();
        isAlert = false;
        ShowInfiltrate();
    }

    private void HandleEnemyDied(EnemyHealth _)
    {
        if (isAlert && !isCountingDown) RefreshEnemyCount();
    }

    // ── Display ───────────────────────────────────────────────────

    private void StopCountdown()
    {
        isCountingDown = false;

        if (nextWaveText != null)
            nextWaveText.gameObject.SetActive(false);

        if (enemyCountText != null)
            enemyCountText.gameObject.SetActive(true);
    }

    private void ShowInfiltrate()
    {
        if (statusText != null)
        {
            statusText.text  = infiltrateLabel;
            statusText.color = infiltrateColor;
        }

        if (waveInfoPanel != null)
            waveInfoPanel.SetActive(false);
    }

    private void ShowAlert()
    {
        if (statusText != null)
        {
            statusText.text  = alertLabel;
            statusText.color = alertColor;
        }

        if (waveInfoPanel != null)
            waveInfoPanel.SetActive(true);

        RefreshWaveDisplay();
    }

    private void RefreshWaveDisplay()
    {
        RefreshWaveText();
        RefreshEnemyCount();
    }

    private void RefreshWaveText()
    {
        if (waveText == null) return;

        int total = generator.TotalWaves;
        int cur   = generator.CurrentWaveNumber;

        waveText.text = total > 0
            ? $"Wave {cur} / {total}"
            : string.Empty;

        waveText.gameObject.SetActive(total > 0);
    }

    private void RefreshEnemyCount()
    {
        if (enemyCountText != null)
            enemyCountText.text = $"Enemies: {generator.AliveEnemiesThisWave}";
    }
}
