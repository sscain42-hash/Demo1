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
        // 🔥 Always reset jump velocity when entering grounded state
        // Ensures clean state for next jump or movement
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

        // 🔥 Chỉ rời Grounded nếu:
        // 1) Player đã airborne đủ stable (>=3 frame)
        // 2) AND velocity rơi xuống (< -0.1f)
        // Điều này tránh chuyển sang Fall do temporary isGrounded=false
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