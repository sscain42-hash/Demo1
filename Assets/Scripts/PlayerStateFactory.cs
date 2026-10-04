public class PlayerStateFactory
{
    private readonly PlayerController _ctx;

    public PlayerStateFactory(PlayerController ctx) => _ctx = ctx;

    // Root states
    public PlayerBaseState Grounded() => new PlayerGroundedState(_ctx, this);
    public PlayerBaseState Jump() => new PlayerJumpState(_ctx, this);
    public PlayerBaseState Fall() => new PlayerFallState(_ctx, this);
    public PlayerBaseState Dash() => new PlayerDashState(_ctx, this);
    public PlayerBaseState Attack() => new PlayerAttackState(_ctx, this);
    public PlayerBaseState Dead() => new PlayerDeadState(_ctx, this);

    // Child states (của Grounded)
    public PlayerBaseState Idle() => new PlayerIdleState(_ctx, this);
    public PlayerBaseState Run() => new PlayerRunState(_ctx, this);
}