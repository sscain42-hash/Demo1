using UnityEngine;

public class PlayerDashState : PlayerBaseState
{
    private float _timer;
    private Vector3 _dashVelocity;

    public override StateCategory Category => StateCategory.Locomotion;

    // 🔥 QUAN TRỌNG: Dash là child của Grounded, KHÔNG phải root
    public bool IsFinished => _timer >= _ctx.DashDuration;

    public PlayerDashState(PlayerController ctx, PlayerStateFactory factory)
        : base(ctx, factory)
    {
        _isRootState = false;   // ← CHILD
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

        // Di chuyển dash
        _ctx.AppliedMovement = new Vector3(_dashVelocity.x, 0f, _dashVelocity.z);

        // Xoay model theo hướng dash
        if (_ctx.Model != null)
        {
            Vector3 dashDir = _ctx.GetHorizontalDashDirection();
            if (dashDir.sqrMagnitude > 0.001f)
                _ctx.Model.rotation = Quaternion.LookRotation(dashDir);
        }

        // Kết thúc dash → tự pop về parent (Grounded)
        if (IsFinished)
        {
            FinishDash();
        }
    }

    private void FinishDash()
    {
        PopToParent();
    }
    protected override void ExitState()
    {
        _ctx.SetRotationLock(false);
    }
}