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

        // 🔥 CHECK JUMP/DASH TRƯỚC — không return sớm
        if (_ctx.TryJump && _ctx.JumpBufferCounter > 0f)
        {
            SwitchState(_factory.Jump());
            return;
        }

        if (_ctx.TryDash)
        {
            SetChildState(_factory.Dash());
            return;
        }

        CheckSwitchToAirborne();

        // Nếu có child (Idle/Run/Dash) → child tự update
        // Không cần check gì thêm
    }

    public override void CheckSwitchState()
    {
        // Để trống hoặc log warning — không dùng nữa
    }

    private void CheckSwitchToAirborne()
    {
        if (_ctx.IsAirborneStable && _ctx.JumpVelocity < -0.1f)
        {
            SwitchState(_factory.Fall());
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