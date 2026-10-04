using UnityEngine;

public class PlayerFallState : PlayerBaseState
{
    private float _fallDurationTimer;
    private const float MAX_FALL_DURATION = 10f; // Safety timeout to prevent infinite fall

    public PlayerFallState(PlayerController ctx, PlayerStateFactory factory)
        : base(ctx, factory)
    {
        _isRootState = true;
    }

    public override void EnterState()
    {
        _fallDurationTimer = 0f;
        _ctx.PlayAnimation(_ctx.Anim_Falling, 0.15f);
    }

    protected override void UpdateState()
    {
        _fallDurationTimer += Time.deltaTime;

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
        // 🔥 PRIORITY 1: Direct CharacterController.isGrounded + small velocity
        // This is the most reliable check for actual ground contact
        if (_ctx.CharController.isGrounded && Mathf.Abs(_ctx.JumpVelocity) < 2f)
        {
            SwitchState(_factory.Grounded());
            return;
        }

        // 🔥 PRIORITY 2: IsGroundedStable (player was grounded for 3+ frames)
        if (_ctx.IsGroundedStable)
        {
            SwitchState(_factory.Grounded());
            return;
        }

        // 🔥 PRIORITY 3: IsGroundedRaw + velocity check (secondary grounded detection)
        if (_ctx.IsGroundedRaw && Mathf.Abs(_ctx.JumpVelocity) < 1f)
        {
            SwitchState(_factory.Grounded());
            return;
        }

        if (_ctx.TryDash)
        {
            SwitchState(_factory.Dash());
            return;
        }

        if (_ctx.TryJump && _ctx.JumpBufferCounter > 0f && _ctx.CoyoteCounter > 0f)
        {
            SwitchState(_factory.Jump());
            return;
        }

        // 🔥 SAFETY: Force transition after max fall duration to prevent infinite stuck
        // This should never trigger in normal gameplay, but catches edge cases
        if (_fallDurationTimer >= MAX_FALL_DURATION)
        {
            Debug.LogWarning("[PlayerFallState] Timeout! Player stuck in Fall state for 10+ seconds. Force transitioning to Grounded.");
            SwitchState(_factory.Grounded());
            return;
        }
    }
}