using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SquadMember : MonoBehaviour
{
    [Header("Squad")]
    [SerializeField] private SquadManager squadManager;
    [SerializeField] private SquadEnemyType enemyType = SquadEnemyType.AR;
    [SerializeField] private EnemyStatus enemyStatus;

    [Header("State")]
    [SerializeField] private bool isAlive = true;

    private readonly List<EnemyStatus> teammateStatusesCache = new List<EnemyStatus>();

    public bool IsAlive
    {
        get { return isAlive; }
    }

    public SquadEnemyType EnemyType
    {
        get { return enemyType; }
    }

    public EnemyStatus Status
    {
        get { return enemyStatus; }
    }

    public SquadManager SquadManager
    {
        get { return squadManager; }
    }

    public IReadOnlyList<EnemyStatus> TeammateStatuses
    {
        get
        {
            RefreshTeammateStatusesCache();
            return teammateStatusesCache;
        }
    }

    public bool IsSniper
    {
        get { return enemyType == SquadEnemyType.Sniper; }
    }

    private void Awake()
    {
        if (enemyStatus == null)
            enemyStatus = GetComponent<EnemyStatus>();

        if (squadManager == null)
            squadManager = GetComponentInParent<SquadManager>();
    }

    private void OnEnable()
    {
        if (squadManager != null)
            squadManager.RegisterMember(this);
    }

    private void OnDisable()
    {
        if (squadManager != null)
            squadManager.UnregisterMember(this);
    }

    public void SetSquadManager(SquadManager manager)
    {
        squadManager = manager;
    }

    public void SetEnemyType(SquadEnemyType type)
    {
        if (enemyType == type)
            return;

        enemyType = type;

        if (squadManager != null)
            squadManager.RefreshMemberTypeLists();
    }

    public void SetAlive(bool alive)
    {
        if (isAlive == alive)
            return;

        isAlive = alive;

        if (squadManager != null)
            squadManager.RefreshSquad();
    }

    public void MarkDead()
    {
        SetAlive(false);
    }

    public void GetTeammateStatuses(List<EnemyStatus> results, bool includeDead = false)
    {
        if (results == null)
            return;

        results.Clear();

        if (squadManager == null)
            return;

        squadManager.GetTeammateStatuses(this, results, includeDead);
    }

    private void RefreshTeammateStatusesCache()
    {
        GetTeammateStatuses(teammateStatusesCache);
    }
}
