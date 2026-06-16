using UnityEngine;

public class PlayerDashState : PlayerBaseState
{
    private const float MOMENTUM_RETENTION = 0.5f;

    private float _timer;
    private Vector3 _dashVelocity;

    public PlayerDashState(PlayerController ctx, PlayerStateFactory factory)
        : base(ctx, factory) { _isRootState = true; }

    public override void EnterState()
    {
        int targetAnim = _ctx.IDDashBack;
        if (_ctx.InputVector.sqrMagnitude > 0.01f) targetAnim = _ctx.Anim_DashBack;
            _ctx.PlayAnimation(targetAnim, 0.05f);
        _ctx.ResetDashCooldown();

        ComputeDashVelocity();
        _timer = 0f;
    }

    private void ComputeDashVelocity()
    {
        Vector3 dashDir = _ctx.GetHorizontalDashDirection();
        float speed = _ctx.DashLength / Mathf.Max(0.0001f, _ctx.DashDuration);
        _dashVelocity = dashDir * speed;

        // Apply initial dash velocity
        _ctx.SetVelocity(_dashVelocity.x, 0f, _dashVelocity.z);
    }

    protected override void UpdateState()
    {
        _timer += Time.deltaTime;

        // Maintain dash velocity and lock Y
        _ctx.SetVelocity(_dashVelocity.x, 0f, _dashVelocity.z);

        if (_timer >= _ctx.DashDuration)
            CheckSwitchState();
        if (_ctx.Model != null)
        {
            Vector3 dashDir = _ctx.GetHorizontalDashDirection();
       
                _ctx.Model.rotation = Quaternion.LookRotation(dashDir);
        }

    }

    protected override void ExitState()
    {


        // Celeste-style momentum retention
        _ctx.SetVelocity(
            _ctx.Velocity.x * MOMENTUM_RETENTION,
            _ctx.Velocity.y,
            _ctx.Velocity.z * MOMENTUM_RETENTION
        );
    }

    public override void CheckSwitchState()
    {
        if (_ctx.CharController.isGrounded)
            SwitchState(_factory.Grounded());
        else
            SwitchState(_factory.Falling());
    }
}
