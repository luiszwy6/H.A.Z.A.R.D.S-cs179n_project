using UnityEngine;

public class SquadAlertState : SquadState
{
    public override SquadBehaviorStateType StateType
    {
        get { return SquadBehaviorStateType.Alert; }
    }

    private float timer;

    public SquadAlertState(SquadBehavior squadBehavior) : base(squadBehavior) { }

    public override void Enter()
    {
        timer = 0f;

        squadManager.SetTacticalOverrideActive(false);
    }

    public override void Tick()
    {
        timer += Time.deltaTime;

        if (timer >= squadBehavior.AlertDuration)
        {
            squadBehavior.ChangeToFight();
            return;
        }
    }

    public override void Exit()
    {
    }
}
