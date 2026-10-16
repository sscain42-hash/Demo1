using UnityEngine;

public class PlayerDashState : PlayerBaseState
{
    private float _timer;
    private Vector3 _dashVelocity;

    public override StateCategory Category => StateCategory.Locomotion;

    public bool IsFinished => _timer >= _ctx.DashDuration;
    public PlayerDashState(PlayerController ctx, PlayerStateFactory factory)
        : base(ctx, factory)
    {
        _isChildState = true;   // 🔥 Đánh dấu là child
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
        _dashVelocity = dashDir * speed;
    }

    protected override void UpdateState()
    {
        _timer += Time.deltaTime;

        _ctx.AppliedMovement = new Vector3(_dashVelocity.x, 0f, _dashVelocity.z);

        if (_ctx.Model != null)
        {
            Vector3 dashDir = _ctx.GetHorizontalDashDirection();
            if (dashDir.sqrMagnitude > 0.001f)
                _ctx.Model.rotation = Quaternion.LookRotation(dashDir);
        }

        if (IsFinished)
        {
            PopToParent();   // 🔥 Về Grounded → InitializeSubState → Idle/Run
        }
    }

    protected override void ExitState()
    {
        _ctx.SetRotationLock(false);
    }
}
