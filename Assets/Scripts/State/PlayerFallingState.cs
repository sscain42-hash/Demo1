using UnityEngine;

public class PlayerFallState : PlayerBaseState
{
    private float _fallDurationTimer;
    private const float MAX_FALL_DURATION = 10f;

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
        // 🔥 PRIORITY 1: CharacterController.isGrounded + small velocity
        if (_ctx.CharController.isGrounded && Mathf.Abs(_ctx.JumpVelocity) < 2f)
        {
            SwitchState(_factory.Grounded());
            return;
        }

        // 🔥 PRIORITY 2: IsGroundedStable (3+ frames grounded)
        if (_ctx.IsGroundedStable)
        {
            SwitchState(_factory.Grounded());
            return;
        }

        // 🔥 PRIORITY 3: IsGroundedRaw + small velocity
        if (_ctx.IsGroundedRaw && Mathf.Abs(_ctx.JumpVelocity) < 1f)
        {
            SwitchState(_factory.Grounded());
            return;
        }

        // Dash cancel
        if (_ctx.TryDash)
        {
            SwitchState(_factory.Dash());
            return;
        }

        // Jump cancel (coyote time)
        if (_ctx.TryJump && _ctx.JumpBufferCounter > 0f && _ctx.CoyoteCounter > 0f)
        {
            SwitchState(_factory.Jump());
            return;
        }

        // Safety timeout
        if (_fallDurationTimer >= MAX_FALL_DURATION)
        {
            Debug.LogWarning("[PlayerFallState] Timeout — force transition to Grounded");
            SwitchState(_factory.Grounded());
            return;
        }
    }
}