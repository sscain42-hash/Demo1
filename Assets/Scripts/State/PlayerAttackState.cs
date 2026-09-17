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
       
                _ctx.Animator.SetLayerWeight(0, 1f);
         
    }

    protected override void UpdateState()
    {
        base.UpdateState();
        CheckSwitchState();
    }

    public override void CheckSwitchState()
    {
        if (_comboManager == null || _hasExited) return;

        Debug.Log(_comboManager.IsAttacking ? "IsAttacking" : "Not Attacking");

        // 1. Dash Cancel
        if (_comboManager.CanDashCancelNow && _ctx.TryDash)
        {
            _hasExited = true;
            _comboManager.ForceCancelCombo(false);
            SwitchState(_factory.Dash());
            return;
        }

        ApplyBaseGravity();

        // 2. Jump Cancel
        if (_comboManager.CanJumpCancelNow && _ctx.IsGrounded && _ctx.TryJump)
        {
            _hasExited = true;
            _comboManager.ForceCancelCombo(false);
            SwitchState(_factory.Jump());
            return;
        }

        // 3. Kết thúc đòn đánh tự nhiên
        if (_comboManager.CurrrentProgressAnimation >= 0.98f)
        {
            _hasExited = true;
            _comboManager.ForceCancelCombo(true);

            // Chuyển sang trạng thái phù hợp theo môi trường thực tế
            if (_characterController.isGrounded)
            {
                SwitchState(_factory.Grounded());
            }
            else
            {
                SwitchState(_factory.Falling());
            }
            return;
        }
    }

    private void ApplyBaseGravity()
    {
        Vector3 gravityVelocity = Vector3.zero;
        gravityVelocity.y = _characterController.isGrounded
            ? -0.5f
            : _ctx.Velocity.y + Physics.gravity.y * Time.deltaTime;

        // Giữ lại lực quán tính chém (XZ) do ComboEngine tạo ra và áp dụng thêm trọng lực Y
        _ctx.Velocity = new Vector3(_ctx.Velocity.x, gravityVelocity.y, _ctx.Velocity.z);
    }

    protected override void ExitState()
    {
        base.ExitState();

        _ctx.SetAttackLock(false); // Mở khóa hệ thống điều khiển di chuyển gốc
                                   // 🔥 TẮT WEIGHT: Trả Weight của Layer UpperBody về 0 khi thoát trạng thái đánh
        if (_ctx.Animator != null)
        {
      
         
                _ctx.Animator.SetLayerWeight(0, 0f);
           
        }
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