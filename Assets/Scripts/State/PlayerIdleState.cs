using UnityEngine;

public class PlayerIdleState : PlayerBaseState
{
    public override StateCategory Category => StateCategory.Locomotion;
    public PlayerIdleState(PlayerController ctx, PlayerStateFactory factory)
        : base(ctx, factory) 
    {
        _isChildState = true;
    }

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