using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class AlertReceiver : MonoBehaviour
{
    [Header("Alert")]
    [Min(0f)] [SerializeField] private float revealDuration = 8f;

    [Tooltip("Delay before alerting, so the entire spawn batch is registered in the squad first.")]
    [Min(0f)] [SerializeField] private float alertDelay = 0.15f;

    private SquadMember squadMember;
    private EnemySensor enemySensor;

    private void Awake()
    {
        squadMember = GetComponent<SquadMember>();
        enemySensor = GetComponent<EnemySensor>();
    }

    private void Start()
    {
        StartCoroutine(AlertRoutine());
    }

    private IEnumerator AlertRoutine()
    {
        if (alertDelay > 0f)
            yield return new WaitForSecondsRealtime(alertDelay);

        AlertSquad();
    }

    private void AlertSquad()
    {
        PlayerStatus playerStatus = PlayerStatus.Instance;

        if (playerStatus == null)
            return;

        Transform playerTransform = playerStatus.transform;

        if (enemySensor != null)
            enemySensor.RevealTargetFromSquad(playerTransform, revealDuration);

        if (squadMember == null || squadMember.SquadManager == null)
            return;

        squadMember.SquadManager.RevealTargetToTeammates(squadMember, playerTransform, revealDuration);
    }
}
