public class PlayerStateFactory
{
    private readonly PlayerController _ctx;

    public PlayerStateFactory(PlayerController ctx) => _ctx = ctx;

    public PlayerBaseState Grounded() => new PlayerGroundedState(_ctx, this);
    public PlayerBaseState Airborne() => new PlayerAirborneState(_ctx, this);
    public PlayerBaseState Jump() => new PlayerJumpState(_ctx, this);
    public PlayerBaseState Fall() => new PlayerFallState(_ctx, this);
    public PlayerBaseState Dash() => new PlayerDashState(_ctx, this);
    public PlayerBaseState AirDash() => new PlayerAirDashState(_ctx, this);

    public PlayerBaseState Attack() => new PlayerAttackState(_ctx, this);
    public PlayerBaseState Dead() => new PlayerDeadState(_ctx, this);
    public PlayerBaseState Hit() => new PlayerHitState(_ctx, this);
    public PlayerBaseState Parry() => new PlayerParryState(_ctx, this);
    public PlayerBaseState ParryMovement() => new PlayerParryMovementState(_ctx, this);

    public PlayerBaseState Idle() => new PlayerIdleState(_ctx, this);
    public PlayerBaseState Run() => new PlayerRunState(_ctx, this);
}