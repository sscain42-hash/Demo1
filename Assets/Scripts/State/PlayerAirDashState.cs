using UnityEngine;

public class PlayerAirDashState : PlayerBaseState
{
    private float _timer;
    private Vector3 _dashVelocity;

    public override StateCategory Category => StateCategory.Locomotion;
    public bool IsFinished => _timer >= _ctx.DashDuration;

    public PlayerAirDashState(PlayerController ctx, PlayerStateFactory factory)
      : base(ctx, factory)
    {
        _isChildState = true;   // 🔥 THÊM
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
        _ctx.SetVelocity(dashDir.x * speed, 0f, dashDir.z * speed);   // y=0 để override gravity
    }

    protected override void UpdateState()
    {
        _timer += Time.deltaTime;
        _ctx.JumpVelocity = 0f;   // Đè gravity trong air dash

        if (_ctx.Model != null)
        {
            Vector3 dashDir = _ctx.GetHorizontalDashDirection();
            if (dashDir.sqrMagnitude > 0.001f)
                _ctx.Model.rotation = Quaternion.LookRotation(dashDir);
        }

        if (IsFinished)
        {
            // Bảo toàn 50% momentum ngang khi kết thúc air dash
            Vector3 v = _ctx.Velocity;
            _ctx.SetVelocity(v.x * 0.5f, _ctx.JumpVelocity, v.z * 0.5f);
            PopToParent();
        }
    }
}
