using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class EnemySquadGenerator : MonoBehaviour
{
    [Header("Squad Manager")]
    [Tooltip("Auto-created if left empty. Each generator gets its own independent SquadManager.")]
    [SerializeField] private SquadManager squadManager;

    [Header("Squad Configs")]
    [SerializeField] private List<SquadConfig> squadConfigs = new();

    [Header("Spawn Area")]
    [Tooltip("Enemy spawns randomly among these points. Leave empty to spawn at this GameObject.")]
    [SerializeField] private List<Transform> spawnPoints = new();
    [SerializeField] private float spawnRadius = 1f;

    [Header("Wave Rules")]
    [Tooltip("Ordered waves. When all waves are cleared, onAllDiedThisRound fires. Leave empty for single-spawn mode.")]
    [SerializeField] private List<WaveRule> waves = new();

    [Header("Stop Conditions (no waves)")]
    [Tooltip("Halt after current round clears. Ignored when Wave Rules are defined.")]
    [SerializeField] private bool stopOnAllDiedThisRound = true;
    [Tooltip("Halt when every spawned enemy is dead.")]
    [SerializeField] private bool stopOnAllClear = false;

    [Header("Spawn Gate")]
    [Tooltip("Block new spawns while current round has alive enemies.")]
    [SerializeField] private bool waitForRoundClear = true;

    [Header("Trigger Spawn Count")]
    [Tooltip("How many squads to spawn per trigger (only used when no Wave Rules are defined).")]
    [Min(1)] [SerializeField] private int spawnCountOnTrigger = 1;

    [Header("Events")]
    public UnityEvent onAllDiedThisRound;
    public UnityEvent onAllClear;

    private readonly List<SquadMember> currentRoundMembers = new();
    private readonly List<SquadMember> allSpawnedMembers = new();

    private bool spawnHalted;
    private bool allDiedThisRoundFired;
    private bool allClearFired;
    private bool hasSpawnedCurrentRound;
    private bool hasSpawnedAny;

    private int currentWaveIndex = -1;
    private Coroutine waveRoutine;
    private List<Transform> activeSpawnPoints;

    private void Awake()
    {
        if (squadManager == null)
        {
            GameObject managerObj = new GameObject($"{gameObject.name}_SquadManager");
            managerObj.transform.SetParent(transform);
            squadManager = managerObj.AddComponent<SquadManager>();
        }
    }

    private void OnEnable()  => EnemyHealth.OnAnyEnemyDied += HandleAnyEnemyDied;
    private void OnDisable()
    {
        EnemyHealth.OnAnyEnemyDied -= HandleAnyEnemyDied;
        if (waveRoutine != null) StopCoroutine(waveRoutine);
    }

    // ── Public API ──────────────────────────────────────────────

    public void SpawnSquad(int configIndex)
    {
        if (configIndex < 0 || configIndex >= squadConfigs.Count)
        {
            Debug.LogWarning($"EnemySquadGenerator: config index {configIndex} out of range.", this);
            return;
        }
        SpawnSquad(squadConfigs[configIndex]);
    }

    public void SpawnSquad(SquadConfig config)
    {
        if (spawnHalted || config == null)
            return;

        if (waitForRoundClear && CountAlive(currentRoundMembers) > 0)
            return;

        currentRoundMembers.Clear();
        allDiedThisRoundFired = false;
        hasSpawnedCurrentRound = true;
        hasSpawnedAny = true;

        SpawnSquadEntries(config);
    }

    // Called per-group inside WaveRoutine; round state is managed by the wave, not here.
    private void SpawnSquadForWave(SquadConfig config)
    {
        if (spawnHalted || config == null)
            return;

        SpawnSquadEntries(config);
    }

    private void SpawnSquadEntries(SquadConfig config)
    {
        foreach (var entry in config.entries)
        {
            if (entry.enemyPrototype == null)
                continue;

            for (int i = 0; i < entry.count; i++)
            {
                GameObject enemy = Instantiate(entry.enemyPrototype, GetSpawnPosition(), Quaternion.identity);
                enemy.SetActive(true);

                SquadMember member = squadManager != null
                    ? squadManager.RegisterEnemy(enemy, entry.enemyType)
                    : enemy.GetComponent<SquadMember>() ?? enemy.AddComponent<SquadMember>();

                if (member == null)
                    continue;

                currentRoundMembers.Add(member);
                allSpawnedMembers.Add(member);
            }
        }
    }

    public void TriggerSpawn(int configIndex)
    {
        if (waves.Count > 0)
        {
            LaunchWave(0);
        }
        else
        {
            for (int i = 0; i < spawnCountOnTrigger; i++)
                SpawnSquad(configIndex);
        }
    }

    public SquadMember GetFirstAliveSpawnedMember()
    {
        for (int i = 0; i < allSpawnedMembers.Count; i++)
        {
            SquadMember m = allSpawnedMembers[i];
            if (m != null && m.IsAlive)
                return m;
        }
        return null;
    }

    public void HaltSpawning() => spawnHalted = true;

    public void ResetGenerator()
    {
        spawnHalted = false;
        allDiedThisRoundFired = false;
        allClearFired = false;
        hasSpawnedCurrentRound = false;
        hasSpawnedAny = false;
        currentWaveIndex = -1;
        activeSpawnPoints = null;
        currentRoundMembers.Clear();
        allSpawnedMembers.Clear();

        if (waveRoutine != null)
        {
            StopCoroutine(waveRoutine);
            waveRoutine = null;
        }
    }

    // ── Wave System ──────────────────────────────────────────────

    private void LaunchWave(int index)
    {
        currentWaveIndex = index;
        allDiedThisRoundFired = true; // block re-entry until wave spawns

        if (waveRoutine != null)
            StopCoroutine(waveRoutine);

        waveRoutine = StartCoroutine(WaveRoutine(waves[index]));
    }

    private IEnumerator WaveRoutine(WaveRule wave)
    {
        if (wave.delayAfterPreviousClear > 0f)
            yield return new WaitForSeconds(wave.delayAfterPreviousClear);

        currentRoundMembers.Clear();
        hasSpawnedAny = true;

        foreach (WaveSpawnGroup group in wave.groups)
        {
            SquadConfig config = group.configIndex >= 0 && group.configIndex < squadConfigs.Count
                ? squadConfigs[group.configIndex]
                : null;

            activeSpawnPoints = group.spawnPoints != null && group.spawnPoints.Count > 0
                ? group.spawnPoints
                : null;

            for (int i = 0; i < group.spawnCount; i++)
                SpawnSquadForWave(config);
        }

        activeSpawnPoints = null;

        // Unlock condition checking only after ALL groups are in currentRoundMembers.
        // LaunchWave sets allDiedThisRoundFired = true to block early triggers;
        // we lower it here once every enemy for this wave is registered.
        hasSpawnedCurrentRound = true;
        allDiedThisRoundFired = false;

        waveRoutine = null;
    }

    // ── Internals ────────────────────────────────────────────────

    private void HandleAnyEnemyDied()
    {
        if (allClearFired)
            return;

        CheckConditions();
    }

    private void CheckConditions()
    {
        bool roundClear = !allDiedThisRoundFired
            && hasSpawnedCurrentRound
            && CountAlive(currentRoundMembers) == 0;

        if (roundClear)
        {
            if (waves.Count > 0)
            {
                int nextWave = currentWaveIndex + 1;

                if (nextWave < waves.Count)
                {
                    LaunchWave(nextWave);
                }
                else
                {
                    allDiedThisRoundFired = true;
                    spawnHalted = true;
                    onAllDiedThisRound.Invoke();
                }
            }
            else if (stopOnAllDiedThisRound)
            {
                allDiedThisRoundFired = true;
                spawnHalted = true;
                onAllDiedThisRound.Invoke();
            }
        }

        if (!allClearFired && stopOnAllClear && hasSpawnedAny
            && CountAlive(allSpawnedMembers) == 0)
        {
            allClearFired = true;
            spawnHalted = true;
            onAllClear.Invoke();
        }
    }

    private static int CountAlive(List<SquadMember> list)
    {
        int alive = 0;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            SquadMember m = list[i];
            if (m == null) { list.RemoveAt(i); continue; }
            if (m.IsAlive) alive++;
        }
        return alive;
    }

    private Vector3 GetSpawnPosition()
    {
        List<Transform> points = activeSpawnPoints ?? spawnPoints;

        List<Transform> valid = new();
        foreach (var p in points)
            if (p != null) valid.Add(p);

        Vector3 center = valid.Count > 0
            ? valid[Random.Range(0, valid.Count)].position
            : transform.position;

        Vector2 offset = Random.insideUnitCircle * spawnRadius;
        return center + new Vector3(offset.x, 0f, offset.y);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.7f, 0f, 0.6f);
        foreach (var p in spawnPoints)
        {
            if (p == null) continue;
            Gizmos.DrawWireSphere(p.position, spawnRadius);
            Gizmos.DrawLine(transform.position, p.position);
        }

        Color[] groupColors = { Color.cyan, Color.green, Color.magenta, Color.red };
        foreach (var wave in waves)
        {
            for (int g = 0; g < wave.groups.Count; g++)
            {
                Gizmos.color = groupColors[g % groupColors.Length];
                foreach (var p in wave.groups[g].spawnPoints)
                {
                    if (p == null) continue;
                    Gizmos.DrawWireSphere(p.position, spawnRadius);
                    Gizmos.DrawLine(transform.position, p.position);
                }
            }
        }
    }
}
