using UnityEngine;

[DisallowMultipleComponent]
public class HealthLight : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private Renderer emissionRenderer;

    [Header("Materials")]
    [SerializeField] private Material highMaterial;
    [SerializeField] private Material mediumMaterial;
    [SerializeField] private Material lowMaterial;

    [Header("Thresholds")]
    [Range(0f, 1f)] [SerializeField] private float highThreshold   = 0.7f;
    [Range(0f, 1f)] [SerializeField] private float mediumThreshold = 0.3f;

    [Header("Update")]
    [Min(0.05f)] [SerializeField] private float refreshInterval = 0.1f;

    private float nextRefreshTime;
    private Material currentMaterial;

    private void Awake()
    {
        if (playerHealth == null)
            playerHealth = GetComponentInParent<PlayerHealth>();

        if (emissionRenderer == null)
            emissionRenderer = GetComponent<Renderer>();
    }

    private void Start()
    {
        Refresh();
    }

    private void Update()
    {
        if (Time.time < nextRefreshTime)
            return;

        Refresh();
    }

    private void Refresh()
    {
        nextRefreshTime = Time.time + refreshInterval;

        if (playerHealth == null || emissionRenderer == null)
            return;

        float ratio = playerHealth.BaseHealth > 0f
            ? playerHealth.CurrentHealth / playerHealth.BaseHealth
            : 0f;

        Material target;

        if (ratio >= highThreshold)
            target = highMaterial;
        else if (ratio >= mediumThreshold)
            target = mediumMaterial;
        else
            target = lowMaterial;

        if (target == null || target == currentMaterial)
            return;

        emissionRenderer.sharedMaterial = target;
        currentMaterial = target;
    }
}
