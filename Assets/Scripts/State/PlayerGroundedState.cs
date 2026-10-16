using UnityEngine;

public class PlayerGroundedState : PlayerBaseState
{
    public PlayerGroundedState(PlayerController ctx, PlayerStateFactory factory)
        : base(ctx, factory)
    {
        _isRootState = true;
    }

    public override void EnterState()
    {
        _ctx.JumpVelocity = -2f;
        InitializeSubState();
    }

    protected override void UpdateState()
    {
        _ctx.HandleRotation();
        CheckSwitchState();
    }

    public override void CheckSwitchState()
    {
        if (_ctx.TryJump && _ctx.JumpBufferCounter > 0f)
        {
            SwitchState(_factory.Jump());
            return;
        }

        if (_ctx.TryDash)
        {
            SwitchState(_factory.Dash());
            return;
        }

        if (_ctx.IsAirborneStable && _ctx.JumpVelocity < -0.1f)
        {
            SwitchState(_factory.Fall());
            return;
        }
    }

    public override void InitializeSubState()
    {
        if (_ctx.InputVector.magnitude < 0.01f)
            SetChildState(_factory.Idle());
        else
            SetChildState(_factory.Run());
    }
}