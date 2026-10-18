using UnityEngine;
using Combat.Parry;

/// <summary>
/// Parry State — phong cách Homura Hime.
/// Chờ kết quả parry (success/fail) trước khi tự thoát.
/// </summary>
public class PlayerParryState : PlayerBaseState,IImplicitTransitionable
{
    public override StateCategory Category => StateCategory.Combat;
    private const float MIN_DURATION = 0.1f;
    private const float MAX_DURATION = 2.0f;
    private const float CANCEL_START = 0.25f;
    private const float NO_RESULT_TIMEOUT = 0.5f;  // safety

    private float _elapsedTime;
    private bool _movementCommitted;
    private bool _parryResultReceived;
    public void RefreshTimer()
    {
        _elapsedTime = 0f;
        _movementCommitted = false;
        _parryResultReceived = false;
    }
    public bool IsFinished
    {
        get
        {
            if (_elapsedTime >= MAX_DURATION) return true;
            if (_elapsedTime < MIN_DURATION) return false;

            // 🔥 Chỉ xong khi parry đã có kết quả (success/fail/timeout)
            return _parryResultReceived;
        }
    }

    public PlayerParryState(PlayerController ctx, PlayerStateFactory factory)
        : base(ctx, factory)
    {
      
    }

    public override void EnterState()
    {
        _elapsedTime = 0f;
        _movementCommitted = false;
        _parryResultReceived = false;

        _ctx.SetAttackLock(true);

        PlayParryAnimation();

        // 🔥 MỞ PARRY WINDOW — bất kể parry được trigger từ đâu
        // (Locomotion, Attack interrupt, Hit interrupt)
        _ctx.ParryController?.OnParryInput();

        var pc = _ctx.ParryController;
        if (pc != null)
        {
            pc.OnParryMovementRequested += HandleMovementRequested;
            pc.OnParryFail += HandleParryFail;
        }
    }
    private void PlayParryAnimation()
    {
        if (_ctx.Animator == null) return;

        var sm = _ctx.GetComponent<PlayerSkillManager>();
        string clipName = sm != null ? sm.GetParryAnimationName() : null;

        if (!string.IsNullOrEmpty(clipName) && _ctx.Animator.HasState(0, Animator.StringToHash(clipName)))
        {
            _ctx.Animator.CrossFadeInFixedTime(clipName, 0.08f, 0, 0f);
            Debug.Log($"[PlayerParryState] Playing '{clipName}'");
        }
        else
        {
            _ctx.Animator.CrossFadeInFixedTime(_ctx.Anim_Parry, 0.08f, 0, 0f);
            Debug.Log("[PlayerParryState] Fallback to Anim_Parry hash");
        }
    }

    protected override void UpdateState()
    {
        _elapsedTime += Time.deltaTime;

        // Safety: nếu qua NO_RESULT_TIMEOUT mà chưa có event → coi như fail
        if (!_parryResultReceived && _elapsedTime >= NO_RESULT_TIMEOUT)
        {
            Debug.LogWarning($"[PlayerParryState] No result after {NO_RESULT_TIMEOUT}s → assume fail");
            _parryResultReceived = true;
        }

        // Cancel bằng Dash/Jump sau delay
        if (_elapsedTime >= CANCEL_START)
        {
            if (_ctx.TryDash) { _ctx.RequestState(StateRequestType.Dash); return; }
            if (_ctx.TryJump) { _ctx.RequestState(StateRequestType.Jump); return; }
        }
    }

    /// <summary>
    /// Parry thành công — nhận movement và request state.
    /// </summary>
    private void HandleMovementRequested(ParryMovementType movement)
    {
        if (_movementCommitted) return;
        _movementCommitted = true;
        _parryResultReceived = true;

        Debug.Log($"[PlayerParryState] Movement committed: {movement}");
        _ctx.RequestParryMovement(movement);
    }

    /// <summary>
    /// Parry thất bại (window hết hạn, spam, telegraph miss).
    /// </summary>
    private void HandleParryFail()
    {
        if (_parryResultReceived) return;
        _parryResultReceived = true;
        Debug.Log("[PlayerParryState] Parry failed → exit");
    }

    protected override void ExitState()
    {
        _ctx.SetAttackLock(false);

        var pc = _ctx.ParryController;
        if (pc != null)
        {
            pc.OnParryMovementRequested -= HandleMovementRequested;
            pc.OnParryFail -= HandleParryFail;
        }

        Debug.Log("[PlayerParryState] Exit");
    }
}
