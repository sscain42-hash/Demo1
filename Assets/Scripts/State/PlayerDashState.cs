using UnityEngine;

public class PlayerDashState : PlayerBaseState
{
    private float _timer;

    public override StateCategory Category => StateCategory.Locomotion;
    public bool IsFinished => _timer >= _ctx.DashDuration;

    public PlayerDashState(PlayerController ctx, PlayerStateFactory factory)
        : base(ctx, factory)
    {
        _isChildState = true;
    }

    public override void EnterState()
    {
        _timer = 0f;
        _ctx._playerInputs.ConsumeCommand(BufferedAction.Dash);
        _ctx.SetRotationLock(true);
        _ctx.PlayAnimation(_ctx.Anim_Dash, 0.05f);
        _ctx.ResetDashCooldown();

        Vector3 dashDir = _ctx.GetHorizontalDashDirection();
        float speed = _ctx.DashLength / Mathf.Max(0.0001f, _ctx.DashDuration);

        // 🔥 Set velocity trực tiếp — dash full speed tức thời
        _ctx.SetVelocity(dashDir.x * speed, _ctx.JumpVelocity, dashDir.z * speed);
    }

    protected override void UpdateState()
    {
        _timer += Time.deltaTime;

        if (_ctx.Model != null)
        {
            Vector3 dashDir = _ctx.GetHorizontalDashDirection();
            if (dashDir.sqrMagnitude > 0.001f)
                _ctx.Model.rotation = Quaternion.LookRotation(dashDir);
        }

        if (IsFinished)
            PopToParent();
    }

    protected override void ExitState()
    {
        _ctx.SetRotationLock(false);

        // 🔥 Bảo toàn 30% momentum — trượt nhẹ sau dash
        Vector3 v = _ctx.Velocity;
        _ctx.SetVelocity(v.x * 0.3f, v.y, v.z * 0.3f);
    }
}
