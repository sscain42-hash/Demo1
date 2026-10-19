using NodeCanvas.Tasks.Actions;
using UnityEngine;

public class PlayerAttackState : PlayerBaseState, IImplicitTransitionable
{
    private readonly PlayerSkillManager _skillManager;
    private float _stateTimer;
    private const float HARD_TIMEOUT = 3f;

    public override StateCategory Category => StateCategory.Combat;

    public bool IsFinished =>
        _stateTimer >= HARD_TIMEOUT ||
        _skillManager == null ||
        !_skillManager.IsAttacking;

    public PlayerAttackState(PlayerController ctx, PlayerStateFactory factory)
        : base(ctx, factory)
    {
        _skillManager = ctx.GetComponent<PlayerSkillManager>();
    }

    public override void EnterState()
    {
        _stateTimer = 0f;
        _ctx.SetAttackLock(true);

        Vector3 v = _ctx.Velocity;
        _ctx.SetVelocity(v.x * 0.3f, v.y, v.z * 0.3f);
     
        _skillManager?.StartPendingCombo();
    }

    protected override void UpdateState()
    {
        _stateTimer += Time.deltaTime;

        // 🔥 Safety net — ép finish nếu state chạy quá lâu
        if (_stateTimer >= HARD_TIMEOUT && _skillManager != null && _skillManager.IsAttacking)
        {
            Debug.LogWarning($"[PlayerAttackState] ⚠️ Kẹt {HARD_TIMEOUT}s — force cancel");
            _skillManager.ForceCancelCombo(false);
        }
    }

    protected override void ExitState()
    {
        _ctx.SetAttackLock(false);
        _skillManager?.OnAttackStateExit();
    }
}