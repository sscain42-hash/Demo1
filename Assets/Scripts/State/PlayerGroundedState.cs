using UnityEngine;

public class PlayerGroundedState : PlayerBaseState
{
    private const float LANDING_VELOCITY_THRESHOLD = -5.0f;

    public PlayerGroundedState(PlayerController ctx, PlayerStateFactory factory)
        : base(ctx, factory) { _isRootState = true; }

    public override void EnterState()
    {
        if (_ctx.Velocity.y < LANDING_VELOCITY_THRESHOLD)
            _ctx.PlayAnimation(_ctx.Anim_Land, 0.1f);
        InitializeSubState();
     
    }

    protected override void UpdateState()
    {

        CheckSwitchState();
    }
    protected override void ExitState() { }

    public override void InitializeSubState()
    {
          SetChildState(_factory.Idle());
    }

    public override void CheckSwitchState()
    {
        // Dash dưới đất
        if ( _ctx.TryDash)
        {
            SwitchState(_factory.Dash());
            return;
        }

        // Jump với coyote + jump buffer
        if (_ctx.JumpBufferCounter > 0f && _ctx.CoyoteCounter > 0f)
        {
            SwitchState(_factory.Jump());
            return;
        }

        // Leave ground
        if (!_ctx.CharController.isGrounded)
            SwitchState(_factory.Falling());
        // Thêm điều kiện chuyển sang Attack
        if (_ctx.TryNormalAttack)
        {
            SwitchState(_factory.Attack());
            return;
        }

    }
}