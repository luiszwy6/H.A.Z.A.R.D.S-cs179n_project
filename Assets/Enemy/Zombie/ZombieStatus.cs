using Unity.Behavior;
using UnityEngine;

[DisallowMultipleComponent]
public class ZombieStatus : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BehaviorGraphAgent behaviorAgent;
    [SerializeField] private EnemyHealth enemyHealth;

    [Header("Detection")]
    [Min(0f)] [SerializeField] private float detectionRange = 10f;
    [Min(0.05f)] [SerializeField] private float checkInterval = 0.2f;

    [Header("Blackboard Variable Names")]
    [SerializeField] private string targetVariableName = "Target";
    [SerializeField] private string isInRangeVariableName = "IsInRangeOfPlayer";
    [SerializeField] private string isDeadVariableName = "IsDead";

    public bool IsDead => enemyHealth != null && enemyHealth.IsDead;
    public bool IsInRangeOfPlayer { get; private set; }

    private float nextCheckTime;

    private void Awake()
    {
        if (behaviorAgent == null)
            behaviorAgent = GetComponent<BehaviorGraphAgent>();

        if (enemyHealth == null)
            enemyHealth = GetComponent<EnemyHealth>();
    }

    private void Start()
    {
        UploadTarget();
        Tick();
    }

    private void Update()
    {
        if (Time.time < nextCheckTime)
            return;

        Tick();
    }

    private void Tick()
    {
        nextCheckTime = Time.time + checkInterval;
        UpdateRangeCheck();
        UploadBlackboard();
    }

    private void UpdateRangeCheck()
    {
        PlayerStatus playerStatus = PlayerStatus.Instance;

        if (playerStatus == null)
        {
            IsInRangeOfPlayer = false;
            return;
        }

        float distance = Vector3.Distance(transform.position, playerStatus.transform.position);
        IsInRangeOfPlayer = distance <= detectionRange;
    }

    private void UploadTarget()
    {
        if (behaviorAgent == null)
            return;

        PlayerStatus playerStatus = PlayerStatus.Instance;

        if (playerStatus != null)
            behaviorAgent.SetVariableValue(targetVariableName, playerStatus.gameObject);
    }

    private void UploadBlackboard()
    {
        if (behaviorAgent == null)
            return;

        behaviorAgent.SetVariableValue(isInRangeVariableName, IsInRangeOfPlayer);
        behaviorAgent.SetVariableValue(isDeadVariableName, IsDead);
    }
}
