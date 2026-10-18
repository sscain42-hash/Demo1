using UnityEngine;

public class PlayerFallState : PlayerBaseState
{
    private const float MAX_FALL_DURATION = 10f;
    private float _timer;

    public override StateCategory Category => StateCategory.Locomotion;

    public PlayerFallState(PlayerController ctx, PlayerStateFactory factory)
        : base(ctx, factory)
    {
        _isChildState = true;
    }

    public override void EnterState()
    {
        _timer = 0f;
        _ctx.PlayAnimation(_ctx.Anim_Falling, 0.15f);
    }

    protected override void UpdateState()
    {
        _timer += Time.deltaTime;

        // Coyote jump
        if (_ctx.TryJump && _ctx.JumpBufferCounter > 0f && _ctx.CoyoteCounter > 0f)
        {
            SwitchState(_factory.Jump());
            return;
        }

        // Safety timeout
        if (_timer >= MAX_FALL_DURATION)
        {
            Debug.LogWarning("[PlayerFallState] Timeout — force transition to Grounded");
            SwitchState(_factory.Grounded());
        }
    }
}
