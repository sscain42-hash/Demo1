using UnityEngine;

public class PlayerAirborneState : PlayerBaseState
{
    protected float _airborneTimer;

    public override StateCategory Category => StateCategory.Locomotion;

    public PlayerAirborneState(PlayerController ctx, PlayerStateFactory factory)
        : base(ctx, factory)
    {
        // Root: _isChildState = false (mặc định)
    }

    public override void EnterState()
    {
        _airborneTimer = 0f;
        InitializeSubState();
    }

    protected override void UpdateState()
    {
        _airborneTimer += Time.deltaTime;

        ApplyAirControl();
        CheckLanding();

        // Nếu CheckLanding đã switch sang Grounded → dừng
        if (_ctx.CurrentState != this) return;

        CheckAirDashRequest();
    }

    public override void InitializeSubState()
    {
        // Có jump buffer → Jump. Ngược lại → Fall.
        if (_ctx.JumpBufferCounter > 0f)
            SetChildState(_factory.Jump());
        else
            SetChildState(_factory.Fall());
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
        if (_ctx.CharController.isGrounded && Mathf.Abs(_ctx.JumpVelocity) < 2f)
        {
            SwitchState(_factory.Grounded());
            return;
        }
        if (_ctx.IsGroundedStable)
        {
            SwitchState(_factory.Grounded());
            return;
        }
        if (_ctx.IsGroundedRaw && Mathf.Abs(_ctx.JumpVelocity) < 1f)
        {
            SwitchState(_factory.Grounded());
            return;
        }
    }

    protected virtual void CheckAirDashRequest()
    {
        if (_ctx.TryDash && !(_childState is PlayerAirDashState))
        {
            if (_childState != null)
            {
                _childState.ForceExit();
                _childState = null;
            }
            SetChildState(_factory.AirDash());
        }
    }
}
