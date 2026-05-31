using UnityEngine;

/// <summary>
/// Run state: player is moving horizontally with responsive curves.
/// Implements "Building Better Movement" pattern with normalized curve functions.
/// </summary>
public class PlayerRunState : PlayerBaseState
{
    private const float BACKWARD_SPEED_MODIFIER = 0.65f;
    private const float IDLE_SPEED_MODIFIER = 0.8f;
    private const float INPUT_THRESHOLD = 0.01f;
    private const float STRAFE_INPUT_THRESHOLD = 0.1f;

    private IMovementHandler _movementHandler;

    public PlayerRunState(PlayerController ctx, PlayerStateFactory factory)
        : base(ctx, factory) 
    {
        _movementHandler = ctx.GroundMovementHandler;
    }

    public override void EnterState()
    {
        int moveAnim = _ctx.GetMovementAnimation();
        _ctx.PlayAnimation(moveAnim, 0.1f);
    }

    protected override void UpdateState()
    {
        Vector3 moveDir = _ctx.GetLookDirection();
        float targetSpeed = CalculateTargetSpeed();

        // Better Movement: Use responsive curve (not linear acceleration)
        Vector3 velocity = _ctx.Velocity;
        _movementHandler.ApplyMovement(moveDir, targetSpeed, ref velocity);
        _ctx.Velocity = velocity;

        _ctx.PlayAnimation(_ctx.GetMovementAnimation());

        CheckSwitchState();
    }

    private float CalculateTargetSpeed()
    {
        float speed = _ctx.RunMaxSpeed;

        
            if (_ctx.InputVector.y < -STRAFE_INPUT_THRESHOLD)
                speed *= BACKWARD_SPEED_MODIFIER;
            else if (Mathf.Abs(_ctx.InputVector.y) < STRAFE_INPUT_THRESHOLD)
                speed *= IDLE_SPEED_MODIFIER;
       

        return speed;
    }

    public override void CheckSwitchState()
    {
        if (_ctx.InputVector.magnitude < INPUT_THRESHOLD)
            SwitchState(_factory.Idle());
    }

    protected override void ExitState() { }
    public override void InitializeSubState() { }
}