using UnityEngine;

public abstract class PlayerAirborneState : PlayerBaseState
{
    protected float _airborneTimer;

    public override StateCategory Category => StateCategory.Locomotion;

    protected PlayerAirborneState(PlayerController ctx, PlayerStateFactory factory)
        : base(ctx, factory)
    {
        _isRootState = true;
    }

    public override void EnterState()
    {
        _airborneTimer = 0f;
        OnAirEnter();
    }

    protected override void UpdateState()
    {
        _airborneTimer += Time.deltaTime;

        ApplyAirControl();

        // Nếu đang có child (AirDash) → để child update, root chỉ check landing
        if (_childState != null)
        {
            CheckLanding();
            return;
        }

        CheckLanding();
        CheckAirDashRequest();
        OnAirUpdate();
    }

    protected virtual void ApplyAirControl()
    {
        if (_ctx.InputVector.sqrMagnitude <= 0.01f) return;

        Vector3 moveDir = _ctx.GetLookDirection();
        Vector3 velocity = _ctx.Velocity;
        _ctx.AirMovementHandler.ApplyAirControl(moveDir, ref velocity);
        _ctx.Velocity = velocity;
    }

    protected virtual void CheckLanding()
    {
        // Priority 1: CharacterController.isGrounded + small velocity
        if (_ctx.CharController.isGrounded && Mathf.Abs(_ctx.JumpVelocity) < 2f)
        {
            SwitchState(_factory.Grounded());
            return;
        }

        // Priority 2: IsGroundedStable
        if (_ctx.IsGroundedStable)
        {
            SwitchState(_factory.Grounded());
            return;
        }

        // Priority 3: IsGroundedRaw + small velocity
        if (_ctx.IsGroundedRaw && Mathf.Abs(_ctx.JumpVelocity) < 1f)
        {
            SwitchState(_factory.Grounded());
            return;
        }
    }

    /// <summary>
    /// Root xử lý Dash request chung cho Jump/Fall.
    /// </summary>
    protected virtual void CheckAirDashRequest()
    {
        if (_ctx.TryDash)
        {
            SetChildState(_factory.AirDash());
        }
    }

    protected abstract void OnAirEnter();
    protected abstract void OnAirUpdate();
}