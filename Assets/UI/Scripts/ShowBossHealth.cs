using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class ShowBossHealth : MonoBehaviour
{
    [Header("Boss References (assign in Inspector)")]
    [SerializeField] private EnemyHealth enemyHealth;
    [SerializeField] private EnemyStatus enemyStatus;

    [Header("UI")]
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private GameObject panel;  // optional root to show/hide; falls back to healthText's GameObject

    [Header("Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color phase2Color  = Color.red;

    [Header("Format")]
    [SerializeField] private string label = "BOSS";

    private bool wasPhase2;

    private void Awake()
    {
        if (enemyStatus == null && enemyHealth != null)
            enemyStatus = enemyHealth.GetComponent<EnemyStatus>();
    }

    private void Start()
    {
        if (healthText != null)
            healthText.color = normalColor;

        SetPanelVisible(false);
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

    private void Update()
    {
        if (enemyHealth == null || healthText == null)
            return;

        bool bossAlive = enemyHealth.gameObject.activeInHierarchy && !enemyHealth.IsDead;
        SetPanelVisible(bossAlive);

        if (!bossAlive)
            return;

        float percent = enemyHealth.BaseHealth > 0f
            ? Mathf.Clamp01(enemyHealth.CurrentHealth / enemyHealth.BaseHealth) * 100f
            : 0f;

        healthText.text = $"{label}  {percent:F0}%";

        bool isPhase2 = enemyStatus != null && enemyStatus.Is2ndPhase;
        if (isPhase2 != wasPhase2)
        {
            healthText.color = isPhase2 ? phase2Color : normalColor;
            wasPhase2 = isPhase2;
        }
    }

    private void OnBossDied()
    {
        SetPanelVisible(false);
    }

    private void SetPanelVisible(bool visible)
    {
        if (panel != null)
            panel.SetActive(visible);
        else if (healthText != null)
            healthText.gameObject.SetActive(visible);
    }
}
