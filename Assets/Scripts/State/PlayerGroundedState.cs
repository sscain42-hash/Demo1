using UnityEngine;

public class PlayerGroundedState : PlayerBaseState
{
    public override StateCategory Category => StateCategory.Locomotion;

    public PlayerGroundedState(PlayerController ctx, PlayerStateFactory factory)
        : base(ctx, factory)
    {
        _isRootState = true;
    }

    public override void EnterState()
    {
        _ctx.JumpVelocity = -2f;
        InitializeSubState();
    }

    protected override void UpdateState()
    {
        _ctx.HandleRotation();

        // ========================================================
        // Nếu đang có child state (Idle/Run/Dash), để child xử lý
        // ========================================================
        if (_childState != null)
        {
            // Child state tự CheckSwitchState trong UpdateStates()
            // Grounded chỉ check transition lên Airborne
            CheckSwitchToAirborne();
            return;
        }

        // Không có child → check ground transitions
        CheckSwitchState();
    }

    public override void CheckSwitchState()
    {
        // Jump (từ Idle/Run)
        if (_ctx.TryJump && _ctx.JumpBufferCounter > 0f)
        {
            SwitchState(_factory.Jump());
            return;
        }

        // Dash (từ Idle/Run) → chuyển thành child Dash
        if (_ctx.TryDash)
        {
            SetChildState(_factory.Dash());
            return;
        }

        // Airborne → thoát khỏi Grounded
        CheckSwitchToAirborne();
    }

    private void CheckSwitchToAirborne()
    {
        // Chỉ chuyển sang Airborne khi:
        // - Rời đất ổn định (> 3 frame)
        // - VÀ đang rơi xuống (jumpVelocity < 0)
        if (_ctx.IsAirborneStable && _ctx.JumpVelocity < -0.1f)
        {
            SwitchState(_factory.Fall());
            return;
        }
    }

    public override void InitializeSubState()
    {
        if (_ctx.InputVector.magnitude < 0.01f)
            SetChildState(_factory.Idle());
        else
            SetChildState(_factory.Run());
    }
}