using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class SquadMember : MonoBehaviour
{
    [Header("Squad")]
    [SerializeField] private SquadManager squadManager;
    [SerializeField] private SquadRole squadRole;

    [Header("Personal Goal")]
    [SerializeField] private Transform personalGoal;

    [Header("Movement Optional")]
    [SerializeField] private bool autoSetAgentDestination = false;
    [SerializeField] private NavMeshAgent agent;

    [Header("State")]
    [SerializeField] private bool isAlive = true;

    public SquadRole StartingRole { get; private set; }
    public SquadRole CurrentRole { get; private set; }

    public bool IsAlive
    {
        get { return isAlive; }
    }

    public SquadRole Role
    {
        get { return squadRole; }
    }

    public Transform PersonalGoal
    {
        get { return personalGoal; }
    }

    public Transform CurrentMoveTarget { get; private set; }
    public bool IsUsingSquadGoal { get; private set; }

    private bool hasMovePosition;
    private Vector3 currentMovePosition;

    public bool IsSniper
    {
        get { return squadRole == SquadRole.Sniper; }
    }

    public Vector3 CurrentMovePosition
    {
        get
        {
            if (hasMovePosition)
                return currentMovePosition;

            if (CurrentMoveTarget != null)
                return CurrentMoveTarget.position;

            return transform.position;
        }
    }

    private void Awake()
    {
        StartingRole = squadRole;
        CurrentRole = squadRole;

        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

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

    public void SetCurrentRole(SquadRole newRole)
    {
        CurrentRole = newRole;
    }

    public void ResetCurrentRole()
    {
        CurrentRole = StartingRole;
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

    public void SetPersonalGoal(Transform goal)
    {
        personalGoal = goal;
    }

    public void SetMoveTarget(Transform target, bool usingSquadGoal)
    {
        CurrentMoveTarget = target;
        IsUsingSquadGoal = usingSquadGoal;
        hasMovePosition = false;

        if (autoSetAgentDestination && agent != null && CurrentMoveTarget != null && isAlive)
        {
            agent.isStopped = false;
            agent.SetDestination(CurrentMoveTarget.position);
        }
    }

    public void SetMovePosition(Vector3 position, bool usingSquadGoal)
    {
        CurrentMoveTarget = null;
        currentMovePosition = position;
        hasMovePosition = true;
        IsUsingSquadGoal = usingSquadGoal;

        if (autoSetAgentDestination && agent != null && isAlive)
        {
            agent.isStopped = false;
            agent.SetDestination(currentMovePosition);
        }
    }

    public void ClearMoveTarget()
    {
        CurrentMoveTarget = null;
        IsUsingSquadGoal = false;
        hasMovePosition = false;

        if (autoSetAgentDestination && agent != null)
            agent.isStopped = true;
    }
}