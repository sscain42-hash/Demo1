using UnityEngine;

public class PlayerIdleState : PlayerBaseState
{
    public PlayerIdleState(PlayerController ctx, PlayerStateFactory factory)
        : base(ctx, factory) { }

    public override void EnterState()
    {
        _ctx.PlayAnimation(_ctx.ID_Idle, 0.2f);
    }

    protected override void UpdateState()
    {
        CheckSwitchState();
    }

    public override void CheckSwitchState()
    {
        if (_ctx.InputVector.magnitude > 0.01f)
            SwitchState(_factory.Run());
    }
}