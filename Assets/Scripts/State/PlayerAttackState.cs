using UnityEngine;

public class PlayerAttackState : PlayerBaseState
{
    private readonly PlayerActionComboManager _comboManager;
    private readonly CharacterController _characterController;

    public PlayerAttackState(PlayerController currentContext, PlayerStateFactory factory)
        : base(currentContext, factory)
    {
        _isRootState = true;
        _comboManager = _ctx.GetComponent<PlayerActionComboManager>();
        _characterController = _ctx.GetComponent<CharacterController>();
    }

    public override void EnterState()
    {
        base.EnterState();
        _ctx.SetAttackLock(true); // Khóa trạng thái di chuyển WASD cơ bản của Controller
    }

    protected override void UpdateState()
    {
        base.UpdateState();

        // 1. Kiểm tra điều kiện chuyển đổi hoặc thoát trạng thái sớm (Cancel)
        CheckSwitchState();

        // 2. Áp dụng trọng lực vật lý nền cho nhân vật khi đang chém
        ApplyBaseGravity();
    }

    public override void CheckSwitchState()
    {
        base.CheckSwitchState();

        if (_comboManager == null) return;

        // KỊCH BẢN 1: Hủy đòn đánh bằng Dash (Dash Cancel)
        // Đọc dữ liệu cửa sổ thời gian thực từ ComboManager thông qua hệ thống Action Window mới
        if (_comboManager.CanDashCancelNow && _ctx.TryDash)
        {
            _comboManager.ForceCancelCombo(); // Ép ComboManager dọn dẹp bộ đệm dữ liệu cũ
            SwitchState(_factory.Dash());      // Hủy ngang động tác, chuyển sang State Dash
            return;
        }

        // KỊCH BẢN 2: Hủy đòn đánh bằng Jump (Jump Cancel)
        if (_comboManager.CanJumpCancelNow && _ctx.JumpBufferCounter > 0)
        {
            _comboManager.ForceCancelCombo();
            SwitchState(_factory.Jump());      // Hủy ngang động tác, chuyển sang State Jump
            return;
        }

        // KỊCH BẢN 3: Chuỗi đòn đánh kết thúc hoàn toàn (Hoạt ảnh chạy hết phim mà không bấm nối Combo)
        if (!_comboManager.IsAttacking)
        {
            SwitchState(_factory.Grounded());
        }
    }

    private void ApplyBaseGravity()
    {
        Vector3 gravityVelocity = Vector3.zero;

        // Giữ nguyên vận tốc Y hiện có (Trọng lực tích lũy) hoặc ghim nhẹ mặt đất nếu đang đứng yên
        gravityVelocity.y = _characterController.isGrounded ? -0.5f : _ctx.Velocity.y + Physics.gravity.y * Time.deltaTime;

        _ctx.Velocity = gravityVelocity;
    }

    protected override void ExitState()
    {
        base.ExitState();

        _ctx.SetAttackLock(false); // Mở khóa cho phép Controller nhận lại điều khiển di chuyển
        _ctx.SetVelocity(0f, _ctx.Velocity.y, 0f); // Triệt tiêu toàn bộ lực quán tính thừa tránh lỗi trượt chân
    }
}