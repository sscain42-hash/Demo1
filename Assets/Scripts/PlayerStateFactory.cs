using UnityEngine;

public class PlayerStateFactory
{
    PlayerController _context;
    public PlayerStateFactory(PlayerController currentContext) => _context = currentContext;

    public PlayerBaseState Grounded() => new PlayerGroundedState(_context, this);
    public PlayerBaseState Jump() => new PlayerJumpingState(_context, this);
    public PlayerBaseState Falling() => new PlayerFallingState(_context, this);
    public PlayerBaseState Idle() => new PlayerIdleState(_context, this);
    public PlayerBaseState Dash() => new PlayerDashState(_context, this);
    public PlayerBaseState Run() => new PlayerRunState(_context, this);
    public PlayerBaseState Attack() => new PlayerAttackState(_context, this);
 
}

