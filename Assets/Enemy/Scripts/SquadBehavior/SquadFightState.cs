public class SquadFightState : SquadState
{
    public override SquadBehaviorStateType StateType
    {
        get { return SquadBehaviorStateType.Fight; }
    }

    public SquadFightState(SquadBehavior squadBehavior) : base(squadBehavior) { }

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
