using UnityEngine;

public class PlayerFallingState : PlayerBaseState
{
    private const float INPUT_THRESHOLD = 0.01f;
    private IMovementHandler _movementHandler;

    public PlayerFallingState(PlayerController ctx, PlayerStateFactory factory)
        : base(ctx, factory)
    {
        _isRootState = true;
        _movementHandler = ctx.AirMovementHandler;
    }

    public override void EnterState()
    {
        _ctx.PlayAnimation(_ctx.Anim_Falling, 0.15f);
    }

    protected override void UpdateState()
    {
        Vector3 moveDir = _ctx.GetLookDirection();
        Vector3 velocity = _ctx.Velocity;

        if (moveDir.sqrMagnitude > INPUT_THRESHOLD)
        {
            _movementHandler.ApplyAirControl(moveDir, ref velocity);
        }

        _ctx.Velocity = velocity;
        CheckSwitchState();
    }

    public override void CheckSwitchState()
    {
        // 🔥 1. ĐƯA LÊN ĐẦU: Ưu tiên Tấn công lên trên cùng (chặn Falling/Landing ngắt lệnh)
        if (_ctx.TryNormalAttack || _ctx.TryElementalSkill || _ctx.TryElementalBurst)
        {
            SwitchState(_factory.Attack());
            return;
        }

        // 2. Kiểm tra Dash trên không
        if (_ctx.TryDash)
        {
            SwitchState(_factory.Dash());
            return;
        }

        // 3. Kiểm tra chạm đất (Chỉ chuyển sang Grounded nếu KHÔNG bấm đánh/dash)
        if (_ctx.CharController.isGrounded)
        {
            SwitchState(_factory.Grounded());
            return;
        }

        // 4. Kiểm tra Jump Buffer / Coyote Time
        if (_ctx.JumpBufferCounter > 0f && _ctx.CoyoteCounter > 0f)
        {
            SwitchState(_factory.Jump());
            return;
        }
    }

    protected override void ExitState() { }
    public override void InitializeSubState() { }
}