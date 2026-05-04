public abstract class SquadState
{
    protected SquadBehavior squadBehavior;
    protected SquadManager squadManager;

    public abstract SquadBehaviorStateType StateType { get; }

    public SquadState(SquadBehavior squadBehavior)
    {
        this.squadBehavior = squadBehavior;
        this.squadManager = squadBehavior.SquadManager;
    }

    public virtual void Enter() { }
    public virtual void Tick() { }
    public virtual void Exit() { }
}