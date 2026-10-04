using UnityEngine;

public class PlayerJumpState : PlayerBaseState
{
    private const float JUMP_RELEASE_MULTIPLIER = 0.5f;

    public PlayerJumpState(PlayerController ctx, PlayerStateFactory factory)
        : base(ctx, factory)
    {
        _isRootState = true;
    }

    public override void EnterState()
    {
        _ctx.PlayAnimation(_ctx.Anim_Jump_Begin, 0.05f);

        _ctx.JumpVelocity = _ctx.InitialJumpVelocity;
        _ctx.JumpBufferCounter = 0f;
        _ctx.CoyoteCounter = 0f;
    }

    protected override void UpdateState()
    {
        if (!_ctx._playerInputs.JumpHeld && _ctx.JumpVelocity > 0f)
            _ctx.JumpVelocity *= JUMP_RELEASE_MULTIPLIER;

        if (_ctx.InputVector.sqrMagnitude > 0.01f)
        {
            Vector3 moveDir = _ctx.GetLookDirection();
            Vector3 velocity = _ctx.Velocity;
            _ctx.AirMovementHandler.ApplyAirControl(moveDir, ref velocity);
            _ctx.Velocity = velocity;
        }

        CheckSwitchState();
    }

    public override void CheckSwitchState()
    {
        if (_ctx.TryDash)
        {
            SwitchState(_factory.Dash());
            return;
        }

        // Hết bay lên → Fall
        if (_ctx.JumpVelocity <= 0f)
        {
            SwitchState(_factory.Fall());
            return;
        }

        // Fallback nếu chạm đất bất ngờ
        if (_ctx.IsGroundedStable)
        {
            SwitchState(_factory.Grounded());
        }
    }
}