using UnityEngine;

public class PlayerAttackState : PlayerBaseState
{
    private readonly PlayerSkillManager _comboManager;
    private readonly CharacterController _characterController;
    private bool _hasExited;

    public PlayerAttackState(PlayerController currentContext, PlayerStateFactory factory)
        : base(currentContext, factory)
    {
        _isRootState = true;
        _comboManager = _ctx.GetComponent<PlayerSkillManager>();
        _characterController = _ctx.GetComponent<CharacterController>();
    }

    public override void EnterState()
    {
        base.EnterState();
        _hasExited = false;
        _ctx.SetAttackLock(true);
    }

    protected override void UpdateState()
    {
        base.UpdateState();
        ApplyBaseGravity();
        CheckSwitchState();
    }

    public override void CheckSwitchState()
    {
        if (_comboManager == null || _hasExited) return;

        // 🎯 2. HỦY ĐÒN SỚM BẰNG DASH
        if (_comboManager.CanDashCancelNow && _ctx.TryDash)
        {
            _hasExited = true;

            // 🔥 SỬA: Truyền 'false' để KHÔNG ép Animator chạy hoạt ảnh Idle, tránh xung đột lệnh làm x2 VFX
            _comboManager.ForceCancelCombo(false);

            SwitchState(_factory.Dash());
            return;
        }

        // 🎯 3. HỦY ĐÒN SỚM BẰNG JUMP
        if (_comboManager.CanJumpCancelNow && _ctx.JumpBufferCounter > 0)
        {
            _hasExited = true;

            // 🔥 SỬA: Truyền 'false' để chặn lỗi giật mốc thời gian Animator gây x2 Hitbox/VFX
            _comboManager.ForceCancelCombo(false);

            SwitchState(_factory.Jump());
            return;
        }

        // 4. CHUYỂN TRẠNG THÁI TỰ NHIÊN KHI HẾT HOẠT ẢNH CHÉM
        if (!_comboManager.IsAttacking)
        {
            _hasExited = true;

            // Khi đòn đánh tự kết thúc tự nhiên, cho phép chạy Idle bình thường
            _comboManager.ForceCancelCombo(true);
            SwitchState(_factory.Grounded());
            return;
        }
    }

    private void ApplyBaseGravity()
    {
        Vector3 gravityVelocity = Vector3.zero;
        gravityVelocity.y = _characterController.isGrounded ? -0.5f : _ctx.Velocity.y + Physics.gravity.y * Time.deltaTime;

        // Giữ lại lực quán tính chém (XZ) do ComboEngine tạo ra và áp dụng thêm trọng lực Y
        _ctx.Velocity = new Vector3(_ctx.Velocity.x, gravityVelocity.y, _ctx.Velocity.z);
    }

    protected override void ExitState()
    {
        base.ExitState();

        _ctx.SetAttackLock(false); // Mở khóa hệ thống điều khiển di chuyển gốc

        if (_ctx.InputVector.sqrMagnitude > 0.01f)
        {
            Vector3 movementDirection = new Vector3(_ctx.InputVector.x, 0f, _ctx.InputVector.y).normalized;
            _ctx.Velocity = movementDirection * _ctx.RunMaxSpeed;
        }
        else
        {
            _ctx.Velocity = new Vector3(0f, _characterController.isGrounded ? -0.5f : _ctx.Velocity.y, 0f);
        }
    }
}