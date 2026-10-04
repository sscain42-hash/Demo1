using UnityEngine;

public class PlayerDeadState : PlayerBaseState
{
    public PlayerDeadState(PlayerController ctx, PlayerStateFactory factory)
        : base(ctx, factory)
    {
        _isRootState = true;
    }

    public override void EnterState()
    {
        // TODO: revival logic
    }

    protected override void UpdateState() { }
    public override void CheckSwitchState() { }
}