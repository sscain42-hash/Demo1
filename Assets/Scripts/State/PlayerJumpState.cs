using UnityEngine;

public class PlayerJumpState : PlayerAirborneState
{
    private const float JUMP_RELEASE_MULTIPLIER = 0.5f;

    public PlayerJumpState(PlayerController ctx, PlayerStateFactory factory)
        : base(ctx, factory) { }

    protected override void OnAirEnter()
    {
        _ctx.PlayAnimation(_ctx.Anim_Jump_Begin, 0.05f);

        _ctx.JumpVelocity = _ctx.InitialJumpVelocity;
        _ctx.JumpBufferCounter = 0f;
        _ctx.CoyoteCounter = 0f;
    }

    protected override void OnAirUpdate()
    {
        // Variable jump height: thả nút → cắt velocity
        if (!_ctx._playerInputs.JumpHeld && _ctx.JumpVelocity > 0f)
            _ctx.JumpVelocity *= JUMP_RELEASE_MULTIPLIER;

        // Khi hết bay lên → Fall
        if (_ctx.JumpVelocity <= 0f)
        {
            SwitchState(_factory.Fall());
        }
    }
}