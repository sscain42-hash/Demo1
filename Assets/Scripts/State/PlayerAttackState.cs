using UnityEngine;

public class PlayerAttackState : PlayerBaseState
{
    private readonly PlayerSkillManager _skillManager;

    public PlayerAttackState(PlayerController ctx, PlayerStateFactory factory)
        : base(ctx, factory)
    {
        _isRootState = true;
        _skillManager = ctx.GetComponent<PlayerSkillManager>();
    }

    public override void EnterState()
    {
        _ctx.SetAttackLock(true);

        // 🔥 Start combo Ở ĐÂY — không sớm hơn
        _skillManager?.StartPendingCombo();
    }

    protected override void UpdateState()
    {
        // Combo update do SkillManager.Update xử lý (chỉ khi CurrentState is PlayerAttackState)
        // Không cần làm gì ở đây
    }

    protected override void ExitState()
    {
        _ctx.SetAttackLock(false);

        // 🔥 Dispose combo Ở ĐÂY — cùng lúc với state
        _skillManager?.OnAttackStateExit();
    }
}