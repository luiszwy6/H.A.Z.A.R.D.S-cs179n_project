public class SquadSearchState : SquadState
{
    public override SquadBehaviorStateType StateType
    {
        get { return SquadBehaviorStateType.Search; }
    }

    public SquadSearchState(SquadBehavior squadBehavior) : base(squadBehavior) { }

    public override void Enter()
    {
        squadManager.SetTacticalOverrideActive(false);
    }

    public override void Tick()
    {
    }

    public override void Exit()
    {
    }
}
