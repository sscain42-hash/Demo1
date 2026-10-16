using UnityEngine;

public class PlayerFallState : PlayerAirborneState
{
    private const float MAX_FALL_DURATION = 10f;

    public PlayerFallState(PlayerController ctx, PlayerStateFactory factory)
        : base(ctx, factory) { }

    protected override void OnAirEnter()
    {
        _ctx.PlayAnimation(_ctx.Anim_Falling, 0.15f);
    }

    protected override void OnAirUpdate()
    {
        // Jump cancel (coyote time)
        if (_ctx.TryJump && _ctx.JumpBufferCounter > 0f && _ctx.CoyoteCounter > 0f)
        {
            SwitchState(_factory.Jump());
            return;
        }

        // Safety timeout
        if (_airborneTimer >= MAX_FALL_DURATION)
        {
            Debug.LogWarning("[PlayerFallState] Timeout — force transition to Grounded");
            SwitchState(_factory.Grounded());
        }
    }
}