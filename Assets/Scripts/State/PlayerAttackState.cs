using UnityEngine;

public class PlayerAttackState : PlayerBaseState,IImplicitTransitionable
{
    private readonly PlayerSkillManager _skillManager;
    public override StateCategory Category => StateCategory.Combat;
    public bool IsFinished => _skillManager == null || !_skillManager.IsAttacking;
    public PlayerAttackState(PlayerController ctx, PlayerStateFactory factory)
        : base(ctx, factory)
    {
        
        _skillManager = ctx.GetComponent<PlayerSkillManager>();
    }

    public override void EnterState()
    {
        _ctx.SetAttackLock(true);

        // 🔥 Dampen momentum — giữ chút quán tính, không snap về 0
        Vector3 v = _ctx.Velocity;
        _ctx.SetVelocity(v.x * 0.3f, v.y, v.z * 0.3f);

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
        _skillManager?.OnAttackStateExit();
    }
}
