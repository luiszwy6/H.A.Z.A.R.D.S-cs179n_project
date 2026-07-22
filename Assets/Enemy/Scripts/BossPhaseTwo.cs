using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class BossPhaseTwo : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyHealth enemyHealth;
    [SerializeField] private EnemyStatus enemyStatus;
    [SerializeField] private MusicManager musicManager;

    [Header("Trigger")]
    [Range(0.01f, 0.99f)]
    [SerializeField] private float healthThreshold = 0.5f;

    [Header("Fire Trail")]
    [Tooltip("Prefab with BossFireZone + trigger collider + particle effect.")]
    [SerializeField] private GameObject firePrefab;
    [Tooltip("Seconds between each fire spawn.")]
    [SerializeField] private float fireSpawnInterval = 1f;
    [Tooltip("How long each fire zone lasts (also set on BossFireZone.lifetime).")]
    [SerializeField] private float fireDuration = 10f;

    [Header("Grenade Throw")]
    [Tooltip("BossGrenadeThrow component on this GameObject. Leave null to skip grenade throws.")]
    [SerializeField] private BossGrenadeThrow grenadeThrow;
    [Tooltip("Seconds between grenade throws in phase 2. Overrides BossGrenadeThrow.throwInterval.")]
    [SerializeField] private float grenadeThrowInterval = 8f;

    private bool triggered;

    private void Awake()
    {
        if (enemyHealth  == null) enemyHealth  = GetComponent<EnemyHealth>();
        if (enemyStatus  == null) enemyStatus  = GetComponent<EnemyStatus>();
        if (musicManager == null) musicManager = FindFirstObjectByType<MusicManager>();
        if (grenadeThrow == null) grenadeThrow = GetComponent<BossGrenadeThrow>();
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

        // if (enemyStatus != null)
        //     enemyStatus.SetIs2ndPhase(true);

        if (musicManager != null)
            musicManager.StartBossPhaseTwoMusic();

        if (firePrefab != null)
            StartCoroutine(FireTrailRoutine());

        if (grenadeThrow != null)
        {
            grenadeThrow.SetThrowInterval(grenadeThrowInterval);
            grenadeThrow.enabled = true;
        }
    }

    private IEnumerator FireTrailRoutine()
    {
        while (!enemyHealth.IsDead)
        {
            SpawnFire();
            yield return new WaitForSeconds(Mathf.Max(0.1f, fireSpawnInterval));
        }
    }

    private void SpawnFire()
    {
        GameObject fire = Instantiate(firePrefab, transform.position, Quaternion.identity);

        BossFireZone zone = fire.GetComponent<BossFireZone>();
        if (zone != null)
            zone.SetLifetime(fireDuration);
    }
}
