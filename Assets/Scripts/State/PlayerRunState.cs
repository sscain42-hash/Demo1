using UnityEngine;

public class PlayerRunState : PlayerBaseState
{
    public PlayerRunState(PlayerController ctx, PlayerStateFactory factory)
        : base(ctx, factory) { }

    public override void EnterState()
    {
        _ctx.PlayAnimation(_ctx.GetMovementAnimation(), 0.1f);
    }

    protected override void UpdateState()
    {
        Vector3 moveDir = _ctx.GetLookDirection();
        _ctx.AppliedMovement = moveDir.normalized * _ctx.RunMaxSpeed;

        _ctx.PlayAnimation(_ctx.GetMovementAnimation());
        CheckSwitchState();
    }

    public override void CheckSwitchState()
    {
        if (_ctx.InputVector.magnitude < 0.01f)
            SwitchState(_factory.Idle());
    }
}