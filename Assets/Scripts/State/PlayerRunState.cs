using UnityEngine;

public class PlayerRunState : PlayerBaseState
{
    public override StateCategory Category => StateCategory.Locomotion;

    public PlayerRunState(PlayerController ctx, PlayerStateFactory factory)
        : base(ctx, factory)
    {
        _isChildState = true;
    }

    public override void EnterState()
    {
        _ctx.PlayAnimation(_ctx.GetMovementAnimation(), 0.1f);
    }

    protected override void UpdateState()
    {
        Vector3 moveDir = _ctx.GetLookDirection();
        Vector3 velocity = _ctx.Velocity;

        // 🔥 Tăng tốc qua handler — không còn snap
        _ctx.GroundMovementHandler.ApplyMovement(moveDir, _ctx.RunMaxSpeed, ref velocity);
        _ctx.Velocity = velocity;

        _ctx.PlayAnimation(_ctx.GetMovementAnimation());
        CheckSwitchState();
    }

    public override void CheckSwitchState()
    {
        if (_ctx.InputVector.magnitude < 0.01f)
            SwitchState(_factory.Idle());
    }
}
