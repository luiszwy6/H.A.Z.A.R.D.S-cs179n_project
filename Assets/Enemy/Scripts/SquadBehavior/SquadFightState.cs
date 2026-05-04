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
        squadManager.SetCancelLeadBy(true);
        squadManager.SetCancelFormation(true);
    }

    public override void Tick()
    {
    }

    public override void Exit()
    {
    }
}