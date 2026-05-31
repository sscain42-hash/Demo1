
using UnityEngine;
public class PlayerJumpingState : PlayerBaseState
{
    private const float JUMP_RELEASE_MULTIPLIER = 0.5f;

    public PlayerJumpingState(PlayerController ctx, PlayerStateFactory factory)
        : base(ctx, factory)
    {
        _isRootState = true;
    }

    public override void EnterState()
    {
        _ctx.PlayAnimation(_ctx.Anim_Jump_Begin, 0.05f);
        ApplyJumpVelocity();
    }

    private void ApplyJumpVelocity()
    {
        _ctx.SetVelocity(
            _ctx.Velocity.x,
            _ctx.InitialJumpVelocity,
            _ctx.Velocity.z
        );

        _ctx.JumpBufferCounter = 0f;
        _ctx.CoyoteCounter = 0f;
    }

    protected override void UpdateState()
    {
        HandleVariableJump();
        HandleAirMovement();
        CheckSwitchState();
    }

    protected override void ExitState() { }

    public override void CheckSwitchState()
    {
        if (_ctx.Velocity.y <= 0f)
        {
            SwitchState(_factory.Falling());
            return;
        }

        if ( _ctx.TryDash)
        {
            SwitchState(_factory.Dash());
        }
    }

    public override void InitializeSubState() { }

    private void HandleVariableJump()
    {
        if (!_ctx._playerInputs.JumpHeld && _ctx.Velocity.y > 0f)
        {
            _ctx.SetVelocityY(_ctx.Velocity.y * JUMP_RELEASE_MULTIPLIER);
        }
    }

    private void HandleAirMovement()
    {
        Vector3 moveDirection = _ctx.GetLookDirection();
        Vector3 currentVelocity = _ctx.Velocity;
        _ctx.AirMovementHandler.ApplyAirControl(moveDirection, ref currentVelocity);
        _ctx.Velocity = currentVelocity;
    }
}

