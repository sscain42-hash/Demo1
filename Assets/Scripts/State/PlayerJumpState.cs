using UnityEngine;

public class PlayerJumpState : PlayerBaseState
{
    private const float JUMP_RELEASE_MULTIPLIER = 0.5f;

    public override StateCategory Category => StateCategory.Locomotion;

    public PlayerJumpState(PlayerController ctx, PlayerStateFactory factory)
        : base(ctx, factory)
    {
        _isChildState = true;
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
        // Variable jump height
        if (!_ctx._playerInputs.JumpHeld && _ctx.JumpVelocity > 0f)
            _ctx.JumpVelocity *= JUMP_RELEASE_MULTIPLIER;

        // Hết bay lên → Fall (cùng parent Airborne)
        if (_ctx.JumpVelocity <= 0f)
        {
            SwitchState(_factory.Fall());
        }
    }
}
