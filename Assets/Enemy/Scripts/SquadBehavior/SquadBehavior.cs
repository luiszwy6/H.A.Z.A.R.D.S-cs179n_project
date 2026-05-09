using UnityEngine;

[DisallowMultipleComponent]
public class SquadBehavior : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SquadManager squadManager;

    [Header("Initial State")]
    [SerializeField] private SquadBehaviorStateType initialState = SquadBehaviorStateType.Search;

    [Header("State Timing")]
    [SerializeField] private float alertDuration = 2.0f;

    [Header("Debug")]
    [SerializeField] private SquadBehaviorStateType currentStateType;
    [SerializeField] private bool debugLogStateChanges = true;

    private SquadState currentState;

    public SquadManager SquadManager
    {
        get { return squadManager; }
    }

    public float AlertDuration
    {
        get { return alertDuration; }
    }

    public SquadBehaviorStateType CurrentStateType
    {
        get { return currentStateType; }
    }

    private void Awake()
    {
        if (squadManager == null)
            squadManager = GetComponent<SquadManager>();
    }

    private void Start()
    {
        ChangeState(CreateState(initialState));
    }

    private void Update()
    {
        if (currentState != null)
            currentState.Tick();
    }

    public void ChangeState(SquadState newState)
    {
        if (newState == null)
            return;

        if (currentState != null)
            currentState.Exit();

        currentState = newState;
        currentStateType = currentState.StateType;

        if (debugLogStateChanges)
            Debug.Log($"{name} Squad State -> {currentStateType}");

        currentState.Enter();
    }

    public void ChangeToSearch()
    {
        ChangeState(new SquadSearchState(this));
    }

    public void ChangeToAlert()
    {
        ChangeState(new SquadAlertState(this));
    }

    public void ChangeToFight()
    {
        ChangeState(new SquadFightState(this));
    }

    public void ReportEnemyDetected()
    {
        if (currentStateType == SquadBehaviorStateType.Fight)
            return;

        ChangeToAlert();
    }

    public void ReportEnemyLost()
    {
        ChangeToSearch();
    }

    private SquadState CreateState(SquadBehaviorStateType stateType)
    {
        switch (stateType)
        {
            case SquadBehaviorStateType.Search:
                return new SquadSearchState(this);

            case SquadBehaviorStateType.Alert:
                return new SquadAlertState(this);

            case SquadBehaviorStateType.Fight:
                return new SquadFightState(this);

            default:
                return new SquadSearchState(this);
        }
    }
}