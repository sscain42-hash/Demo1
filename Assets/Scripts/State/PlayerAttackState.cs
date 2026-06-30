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

        // 🎯 1. HỦY ĐÒN SỚM BẰNG NÚT DI CHUYỂN (WASD)
        // Nếu cửa sổ nhận Combo Input đang mở VÀ người chơi đang chủ động bấm nút di chuyển
        if (_comboManager.IsAttacking && _ctx.InputVector.sqrMagnitude > 0.01f)
        {
            _hasExited = true;
            _comboManager.ForceCancelCombo(); // Ngắt combo hiện tại
            SwitchState(_factory.Grounded()); // Chuyển ngay lập tức về trạng thái di chuyển tự do
            return;
        }

        // 🎯 2. HỦY ĐÒN SỚM BẰNG DASH
        if (_comboManager.CanDashCancelNow && _ctx.TryDash)
        {
            _hasExited = true;
            _comboManager.ForceCancelCombo();
            SwitchState(_factory.Dash());
            return;
        }

        // 🎯 3. HỦY ĐÒN SỚM BẰNG JUMP
        if (_comboManager.CanJumpCancelNow && _ctx.JumpBufferCounter > 0)
        {
            _hasExited = true;
            _comboManager.ForceCancelCombo();
            SwitchState(_factory.Jump());
            return;
        }

        // 4. CHUYỂN TRẠNG THÁI TỰ NHIÊN KHI HẾT HOẠT ẢNH CHÉM
        if (!_comboManager.IsAttacking)
        {
            _hasExited = true;
            _comboManager.ForceCancelCombo();
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

        if (_ctx.Animator != null)
        {
            _ctx.Animator.StopPlayback();
            _ctx.Animator.Update(0f);
        }

        // Khi thoát trạng thái, nạp ngay hướng di chuyển WASD vào vận tốc để nhân vật mượt mà chạy tiếp
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