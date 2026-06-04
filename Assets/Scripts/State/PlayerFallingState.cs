using UnityEngine;

/// <summary>
/// Falling state: handles air movement with responsive curves.
/// Implements momentum preservation + responsive acceleration.
/// </summary>
public class PlayerFallingState : PlayerBaseState
{
    private const float INPUT_THRESHOLD = 0.01f;

    private IMovementHandler _movementHandler;
    private ResponsiveDecelerationHandler _decelerationHandler;

    public PlayerFallingState(PlayerController ctx, PlayerStateFactory factory)
        : base(ctx, factory) 
    { 
        _isRootState = true;
        _movementHandler = ctx.AirMovementHandler;
    }

    public override void EnterState()
    {
        _ctx.PlayAnimation(_ctx.Anim_Falling, 0.15f);
    }

    protected override void UpdateState()
    {
        Vector3 moveDir = _ctx.GetLookDirection();
        
        // Better Movement: Apply responsive air control
        Vector3 velocity = _ctx.Velocity;
        
        if (moveDir.sqrMagnitude > INPUT_THRESHOLD)
        {
            _movementHandler.ApplyAirControl(moveDir, ref velocity);
        }
        else
        {
            // Decelerate smoothly if no input
            // Note: Initialize deceleration handler here if needed
            // For now, maintain current velocity in air
        }
        
        _ctx.Velocity = velocity;

        CheckSwitchState();
    }

    public override void CheckSwitchState()
    {
        if (_ctx.CharController.isGrounded)
        {
            SwitchState(_factory.Grounded());
            return;
        }

        if (_ctx.JumpBufferCounter > 0f && _ctx.CoyoteCounter > 0f)
        {
            SwitchState(_factory.Jump());
            return;
        }

        if ( _ctx.TryDash)
            SwitchState(_factory.Dash());
    }

    protected override void ExitState() { }
    public override void InitializeSubState() { }
}