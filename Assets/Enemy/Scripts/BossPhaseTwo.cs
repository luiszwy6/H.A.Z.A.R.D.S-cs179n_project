using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Attach to the boss. When health drops below the threshold:
///   - MusicManager switches to phase-two music
///   - Boss gains infinite ammo (no reload)
///   - Boss stops moving (NavMeshAgent speed = 0)
/// </summary>
[DisallowMultipleComponent]
public class BossPhaseTwo : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyHealth enemyHealth;
    [SerializeField] private EnemyWeaponSettings weaponSettings;
    [SerializeField] private EnemyStatus enemyStatus;
    [SerializeField] private MusicManager musicManager;

    [Header("Trigger")]
    [Range(0.01f, 0.99f)]
    [SerializeField] private float healthThreshold = 0.5f;

    private NavMeshAgent agent;
    private bool triggered;

    private void Awake()
    {
        if (enemyHealth    == null) enemyHealth    = GetComponent<EnemyHealth>();
        if (weaponSettings == null) weaponSettings = GetComponentInChildren<EnemyWeaponSettings>();
        if (enemyStatus    == null) enemyStatus    = GetComponent<EnemyStatus>();
        if (musicManager   == null) musicManager   = FindFirstObjectByType<MusicManager>();

        agent = GetComponent<NavMeshAgent>();
    }

    private void Update()
    {
        if (triggered || enemyHealth == null || enemyHealth.IsDead)
            return;

        float percent = enemyHealth.BaseHealth > 0f
            ? enemyHealth.CurrentHealth / enemyHealth.BaseHealth
            : 0f;

        if (percent <= healthThreshold)
            TriggerPhaseTwo();
    }

    private void TriggerPhaseTwo()
    {
        triggered = true;

        // Stop movement
        if (agent != null)
        {
            agent.ResetPath();
            agent.speed = 0f;
        }

        // Flag for behavior graph
        if (enemyStatus != null)
            enemyStatus.SetIs2ndPhase(true);

        // Infinite ammo — no more reloading
        if (weaponSettings != null)
            weaponSettings.SetInfiniteAmmo();

        // Switch music
        if (musicManager != null)
            musicManager.StartBossPhaseTwoMusic();
    }
}
