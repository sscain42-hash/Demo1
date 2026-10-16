using UnityEngine;
/// <summary>
/// AirDash — child của Airborne. Hành vi tương tự Dash nhưng:
/// - Không bị gravity kéo trong lúc dash.
/// - Có thể giữ momentum (velocity ngang) sau khi dash xong.
/// - Pop về parent (Airborne) khi xong → về Jump/Fall tùy velocity.
/// </summary>
public class PlayerAirDashState : PlayerBaseState
{
    private float _timer;
    private Vector3 _dashVelocity;

    public override StateCategory Category => StateCategory.Locomotion;

    public bool IsFinished => _timer >= _ctx.DashDuration;

    public PlayerAirDashState(PlayerController ctx, PlayerStateFactory factory)
        : base(ctx, factory)
    {
       
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

        // Trong lúc air dash: dùng velocity riêng, đè gravity
        _ctx.AppliedMovement = new Vector3(_dashVelocity.x, 0f, _dashVelocity.z);

        // Giữ jumpVelocity = 0 trong air dash (không bay lên, không rơi)
        _ctx.JumpVelocity = 0f;

        if (_ctx.Model != null)
        {
            Vector3 dashDir = _ctx.GetHorizontalDashDirection();
            if (dashDir.sqrMagnitude > 0.001f)
                _ctx.Model.rotation = Quaternion.LookRotation(dashDir);
        }

        if (IsFinished)
        {
            // Set velocity ngang để momentum tiếp tục sau khi dash
            _ctx.SetVelocity(_dashVelocity.x * 0.5f, _ctx.JumpVelocity, _dashVelocity.z * 0.5f);

            // Pop về Airborne parent → sẽ chuyển sang Jump/Fall tùy velocity
            PopToParent();
        }
    }

    protected override void ExitState()
    {
        _ctx.SetRotationLock(false);
    }
}