using UnityEngine;

public class PlayerIdleState : PlayerBaseState
{
    private const float INPUT_THRESHOLD = 0.01f;

    public PlayerIdleState(PlayerController ctx, PlayerStateFactory factory)
        : base(ctx, factory) { }

    public override void EnterState()
    {
        _ctx.PlayAnimation(_ctx.ID_Idle, 0.1f);
    }

    protected override void UpdateState()
    {
        // Stop horizontal movement
        _ctx.SetVelocity(_ctx.Velocity.x * 0f, _ctx.Velocity.y, _ctx.Velocity.z * 0f);
        CheckSwitchState();
    }

    protected override void ExitState() { }

    public override void CheckSwitchState()
    {
        if (_ctx.InputVector.magnitude > INPUT_THRESHOLD)
            SwitchState(_factory.Run());
      
    }

    public override void InitializeSubState() { }       
}