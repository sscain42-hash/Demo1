using UnityEngine;
/// <summary>
/// Root abstract cho mọi state trên không: Jump, Fall.
/// Quản lý:
/// - Coyote time
/// - Air control
/// - Chuyển Jump ↔ Fall
/// - Landing detection → quay về Grounded
/// </summary>
public abstract class PlayerAirborneState : PlayerBaseState
{
    protected float _airborneTimer;

    public override StateCategory Category => StateCategory.Locomotion;

    protected PlayerAirborneState(PlayerController ctx, PlayerStateFactory factory)
        : base(ctx, factory)
    {
        _isRootState = true;
    }

    public override void EnterState()
    {
        _airborneTimer = 0f;
        OnAirEnter();
    }

    protected override void UpdateState()
    {
        _airborneTimer += Time.deltaTime;

        // === AIR CONTROL chung ===
        ApplyAirControl();

        // === TRANSITION chung ===
        CheckCommonTransitions();

        // === Subclass update ===
        OnAirUpdate();
    }

    protected virtual void ApplyAirControl()
    {
        if (_ctx.InputVector.sqrMagnitude <= 0.01f) return;

        Vector3 moveDir = _ctx.GetLookDirection();
        Vector3 velocity = _ctx.Velocity;
        _ctx.AirMovementHandler.ApplyAirControl(moveDir, ref velocity);
        _ctx.Velocity = velocity;
    }

    /// <summary>
    /// Transition rules chung cho mọi state trên không.
    /// Subclass có thể override để thêm logic riêng.
    /// </summary>
    protected virtual void CheckCommonTransitions()
    {
        // 1. Landing → về Grounded
        if (ShouldLand())
        {
            SwitchState(_factory.Grounded());
            return;
        }

        // 2. Dash cancel (chỉ khi Grounded)
        // Note: Dash là child của Grounded, nên chỉ được khi chạm đất
        // Nhưng ở đây đang trên không → không cho dash
        // (Nếu muốn air dash sau này, thêm AirDash state)

        // 3. Jump cancel (coyote time) — chỉ từ Fall mới cho phép
        // Jump → Fall đã tự xử lý, nên ở đây không cần
    }

    protected virtual bool ShouldLand()
    {
        if (_ctx.CharController.isGrounded && Mathf.Abs(_ctx.JumpVelocity) < 2f)
            return true;

        if (_ctx.IsGroundedStable)
            return true;

        if (_ctx.IsGroundedRaw && Mathf.Abs(_ctx.JumpVelocity) < 1f)
            return true;

        return false;
    }

    protected abstract void OnAirEnter();
    protected abstract void OnAirUpdate();
}