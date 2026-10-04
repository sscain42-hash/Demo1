using UnityEngine;

public class PlayerAttackState : PlayerBaseState
{
    private readonly PlayerSkillManager _comboManager;
    private bool _hasExited;

    private float _elapsedTime;
    private const float MAX_ATTACK_DURATION = 3f;
    private const int ATTACK_LAYER = 1;

    public PlayerAttackState(PlayerController ctx, PlayerStateFactory factory)
        : base(ctx, factory)
    {
        _isRootState = true;
        _comboManager = ctx.GetComponent<PlayerSkillManager>();
    }

    public override void EnterState()
    {
        _hasExited = false;
        _elapsedTime = 0f;
        _ctx.SetAttackLock(true);

    }

    protected override void UpdateState()
    {
        _elapsedTime += Time.deltaTime;
        CheckSwitchState();
    }

    public override void CheckSwitchState()
    {
        if (_hasExited) return;

        if (_elapsedTime >= MAX_ATTACK_DURATION)
        {
            ForceExit();
            return;
        }

        if (_comboManager == null)
        {
            ForceExit();
            return;
        }

        // Dash Cancel
        if (_comboManager.CanDashCancelNow && _ctx.TryDash)
        {
            _comboManager.ForceCancelCombo(false);
            SwitchState(_factory.Dash());
            return;
        }

        // Jump Cancel
        if (_comboManager.CanJumpCancelNow && _ctx.IsGroundedRaw && _ctx.TryJump)
        {
            _comboManager.ForceCancelCombo(false);
            SwitchState(_factory.Jump());
            return;
        }

        // 🔥 FIX: Kết thúc tự nhiên — CHỈ ForceExit, KHÔNG ForceCancelCombo
        // PlayerSkillManager sẽ tự FinishComboAttack khi GetNormalizedTime >= 1
        if (_comboManager.CurrrentProgressAnimation >= 0.98f)
        {
            ForceExit();
            return;
        }
    }

    private void ForceExit()
    {
        if (_hasExited) return;

        _hasExited = true;
        _ctx.SetAttackLock(false);

        // 🔥 CRITICAL: Stop combo engine velocity BEFORE state transition
        // This ensures new state gets clean grounded state without attack velocity
        if (_comboManager != null && _comboManager.IsAttacking)
        {
            Debug.Log($"[AttackState Exit] Force stopping attack velocity");
            _comboManager.ForceCancelCombo(false); // Don't play idle - state will handle animation
        }

        // Now state transition will happen with clean velocity
        Debug.Log($"[AttackState Exit] CharController.isGrounded={_ctx.CharController.isGrounded}, " +
                  $"JumpVelocity={_ctx.JumpVelocity}, " +
                  $"GroundedFrameCount={_ctx.GetGroundedFrameCount()}, " +
                  $"IsGroundedRaw={_ctx.IsGroundedRaw}, " +
                  $"IsGroundedStable={_ctx.IsGroundedStable}");

        PlayerBaseState nextState = _ctx.ResolveGroundState();
        Debug.Log($"[AttackState Exit] ResolveGroundState returned: {nextState.GetType().Name}");

        SwitchState(nextState);
    }

    protected override void ExitState()
    {
        _hasExited = true;
        _ctx.SetAttackLock(false);
    }
}