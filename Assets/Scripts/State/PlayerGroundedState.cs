public class PlayerGroundedState : PlayerBaseState
{
    public override StateCategory Category => StateCategory.Locomotion;

    public PlayerGroundedState(PlayerController ctx, PlayerStateFactory factory)
        : base(ctx, factory) { }

    public override void EnterState()
    {
        _ctx.JumpVelocity = -2f;
        InitializeSubState();
    }

    protected override void UpdateState()
    {
        _ctx.HandleRotation();

        if (_ctx.TryJump && _ctx.JumpBufferCounter > 0f)
        {
            SwitchState(_factory.Airborne());   // 🔥 ĐỔI
            return;
        }

        if (_ctx.TryDash)
        {
            SetChildState(_factory.Dash());
            return;
        }

        CheckSwitchToAirborne();
    }

    public override void CheckSwitchState() { }

    private void CheckSwitchToAirborne()
    {
        if (_ctx.IsAirborneStable && _ctx.JumpVelocity < -0.1f)
        {
            SwitchState(_factory.Airborne());   // 🔥 ĐỔI
        }
    }

    public override void InitializeSubState()
    {
        if (_ctx.InputVector.magnitude < 0.01f)
            SetChildState(_factory.Idle());
        else
            SetChildState(_factory.Run());
    }
}
