using Combat.Parry;
using UnityEngine;

public class PlayerParryState : PlayerBaseState, IImplicitTransitionable, IExclusiveVelocityProvider
{
    public override StateCategory Category => StateCategory.Combat;

    private const float MIN_DURATION = 0.1f;
    private const float MAX_DURATION = 2.0f;
    private const float CANCEL_START = 0.25f;
    private const float NO_RESULT_TIMEOUT = 0.5f;

    private float _elapsedTime;
    private bool _movementCommitted;
    private bool _parryResultReceived;

    // =========================================================
    // IExclusiveVelocityProvider — trả về ZERO để đứng yên
    // =========================================================
    public Vector3 GetVelocityModifier() => Vector3.zero;
    public bool IsActive => true;
    public int Priority => 100;

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
            return _parryResultReceived;
        }
    }

    public PlayerParryState(PlayerController ctx, PlayerStateFactory factory)
        : base(ctx, factory) { }

    public override void EnterState()
    {
        _elapsedTime = 0f;
        _movementCommitted = false;
        _parryResultReceived = false;

        _ctx.SetAttackLock(true);

        // 🔥 Zero velocity NGAY — không còn quán tính
        _ctx.SetVelocity(0f, _ctx.JumpVelocity, 0f);

        // 🔥 Register làm exclusive provider — trả về 0 → đứng yên
        // Bypass gravity, bypass fallback movement, bypass mọi provider khác
        _ctx.RegisterVelocityProvider(this);

        PlayParryAnimation();

        var pc = _ctx.ParryController;
        if (pc != null)
        {
            pc.OnParryMovementRequested += HandleMovementRequested;
            pc.OnParryFail += HandleParryFail;
        }

        pc?.OnParryInput();
    }

    private void PlayParryAnimation()
    {
        if (_ctx.Animator == null) return;

        var sm = _ctx.GetComponent<PlayerSkillManager>();
        string clipName = sm != null ? sm.GetParryAnimationName() : null;

        if (!string.IsNullOrEmpty(clipName) && _ctx.Animator.HasState(0, Animator.StringToHash(clipName)))
        {
            _ctx.Animator.CrossFadeInFixedTime(clipName, 0.05f, 0, 0f);
            Debug.Log($"[PlayerParryState] Playing '{clipName}'");
        }
        else
        {
            _ctx.Animator.CrossFadeInFixedTime(_ctx.Anim_Parry, 0.05f, 0, 0f);
            Debug.Log("[PlayerParryState] Fallback to Anim_Parry hash");
        }
    }

    protected override void UpdateState()
    {
        _elapsedTime += Time.deltaTime;

        if (!_parryResultReceived && _elapsedTime >= NO_RESULT_TIMEOUT)
        {
            Debug.LogWarning($"[PlayerParryState] No result after {NO_RESULT_TIMEOUT}s → assume fail");
            _parryResultReceived = true;
        }

        if (_elapsedTime >= CANCEL_START)
        {
            if (_ctx.TryDash) { _ctx.RequestState(StateRequestType.Dash); return; }
            if (_ctx.TryJump) { _ctx.RequestState(StateRequestType.Jump); return; }
        }
    }

    /// <summary>
    /// Parry thành công — chỉ set flag, KHÔNG di chuyển.
    /// </summary>
    private void HandleMovementRequested(ParryMovementType movement)
    {
        if (_movementCommitted) return;
        _movementCommitted = true;
        _parryResultReceived = true;

        Debug.Log($"[PlayerParryState] Parry success: {movement} — stay in place");
    }

    private void HandleParryFail()
    {
        if (_parryResultReceived) return;
        _parryResultReceived = true;
        Debug.Log("[PlayerParryState] Parry failed → exit");
    }

    protected override void ExitState()
    {
        _ctx.SetAttackLock(false);

        // 🔥 Gỡ exclusive provider
        _ctx.UnregisterVelocityProvider(this);

        // 🔥 Zero velocity khi exit — không giữ momentum sau parry
        _ctx.SetVelocity(0f, _ctx.JumpVelocity, 0f);

        var pc = _ctx.ParryController;
        if (pc != null)
        {
            pc.OnParryMovementRequested -= HandleMovementRequested;
            pc.OnParryFail -= HandleParryFail;
        }

        Debug.Log("[PlayerParryState] Exit");
    }
}