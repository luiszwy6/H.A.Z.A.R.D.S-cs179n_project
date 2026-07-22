using UnityEngine;

[DisallowMultipleComponent]
public class EnemyAlertSpawner : MonoBehaviour
{
    [Header("Generator")]
    [SerializeField] private EnemySquadGenerator generator;
    [SerializeField] private int configIndex = 0;
    [Tooltip("Uncheck on boss enemies — being spotted by the boss should not start spawning.")]
    [SerializeField] private bool triggerSpawnOnAlert = true;

    [Header("Member Alert")]
    [Tooltip("How long the revealed position lasts for the first spawned squad member.")]
    [SerializeField] private float memberRevealDuration = 5f;

    private EnemyStatus enemyStatus;
    private EnemySensor enemySensor;

    private void Awake()
    {
        enemyStatus = GetComponent<EnemyStatus>();
        enemySensor  = GetComponent<EnemySensor>();
    }

    private void Update()
    {
        if (enemyStatus == null || !enemyStatus.CanSeeTarget)
            return;

        Trigger();
    }

    private void Trigger()
    {
        if (generator != null)
        {
            if (triggerSpawnOnAlert && !generator.IsStartingGenerate)
                generator.TriggerSpawn(configIndex);

            SquadMember member = generator.GetFirstAliveSpawnedMember();
            if (member != null)
            {
                EnemySensor sensor = member.GetComponent<EnemySensor>();
                Transform target = enemySensor != null ? enemySensor.Target : null;

                if (sensor != null && target != null)
                    sensor.RevealTargetFromSquad(target, memberRevealDuration);
            }
        }

        enabled = false;
    }
}
