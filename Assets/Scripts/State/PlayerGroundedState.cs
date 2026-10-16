using UnityEngine;

public class PlayerGroundedState : PlayerBaseState
{
    public override StateCategory Category => StateCategory.Locomotion;

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

        // Nếu có child (Idle/Run/Dash) → để child tự update
        // Grounded chỉ check transition lên Airborne
        if (_childState != null)
        {
            CheckSwitchToAirborne();
            return;
        }

        // Không có child → check ground transitions
        CheckSwitchState();
    }

    public override void CheckSwitchState()
    {
        // Jump
        if (_ctx.TryJump && _ctx.JumpBufferCounter > 0f)
        {
            SwitchState(_factory.Jump());
            return;
        }

        // Dash → gán child
        if (_ctx.TryDash)
        {
            SetChildState(_factory.Dash());
            return;
        }

        // Airborne
        CheckSwitchToAirborne();
    }

    private void CheckSwitchToAirborne()
    {
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