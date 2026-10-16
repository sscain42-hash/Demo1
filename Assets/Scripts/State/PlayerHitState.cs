using UnityEngine;

public class PlayerHitState : PlayerBaseState
{
    private const float CANCEL_START = 0.1f;
    private const float HARD_TIMEOUT = 2.5f; // Chốt an toàn tuyệt đối
    public override StateCategory Category => StateCategory.Override;
    private float _elapsedTime;

    public bool IsFinished => _elapsedTime >= _ctx.HitStunDuration || _elapsedTime >= HARD_TIMEOUT;

    public PlayerHitState(PlayerController ctx, PlayerStateFactory factory)
        : base(ctx, factory)
    {
        _isRootState = true;
    }

    public override void EnterState()
    {
        _elapsedTime = 0f;
        _ctx.SetAttackLock(true);
        _ctx.SetRotationLock(true);
        _ctx.PlayAnimation(_ctx.HitReactionAnimName, 0.05f);
        _ctx._playerInputs?.ClearBuffer();
    }

    protected override void UpdateState()
    {
        _elapsedTime += Time.deltaTime;

        var inputs = _ctx._playerInputs;
        if (inputs == null) return;

        // Parry interrupt — ưu tiên cao nhất
        if (inputs.HasCommand(BufferedAction.Parry))
        {
            inputs.ConsumeCommand(BufferedAction.Parry);
            _ctx.RequestState(StateRequestType.Parry);
            return;
        }

        // Dash cancel sau delay
        if (_elapsedTime >= CANCEL_START && _ctx.TryDash)
        {
            inputs.ConsumeCommand(BufferedAction.Dash);
            _ctx.RequestState(StateRequestType.Dash);
            return;
        }
    }

    protected override void ExitState()
    {
        _ctx.SetAttackLock(false);
        _ctx.SetRotationLock(false);
    }
}