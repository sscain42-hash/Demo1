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
        _ctx.SetVelocity(0f, _ctx.JumpVelocity, 0f);

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
        string clipName = sm != null ? sm.GetParryAnimationName(ParryMovementType.None) : null;
        float clipLength = sm != null ? sm.GetParryClipLength(ParryMovementType.None) : 0f;

        int parryLayer = PlayerController.Layer_Parry;
        _ctx.Animator.SetLayerWeight(parryLayer, 1f);

        if (string.IsNullOrEmpty(clipName) || clipLength <= 0f)
        {
            Debug.LogWarning("[PlayerParryState] Không có parry clip — bỏ qua animation.");
            return;
        }

        _ctx.Animator.CrossFadeInFixedTime(clipName, 0.05f, parryLayer, 0f);

        // Auto-speed theo MIN_DURATION
        float speed = clipLength / Mathf.Max(0.01f, MIN_DURATION);
        _ctx.Animator.speed = Mathf.Clamp(speed, 0.1f, 10f);
    }

    protected override void UpdateState()
    {
        _elapsedTime += Time.deltaTime;

        // 🔥 Nếu window đã đóng mà chưa có result → thoát ngay
        var pc = _ctx.ParryController;
        if (!_parryResultReceived && pc != null
            && pc.CurrentState != Combat.Parry.ParryState.WindowOpen
            && _elapsedTime > 0.05f)
        {
            _parryResultReceived = true;
            Debug.Log("[PlayerParryState] Window closed without success → exit");
        }

        if (_elapsedTime >= CANCEL_START)
        {
            if (_ctx.TryDash) { _ctx.RequestState(StateRequestType.Dash); return; }
            if (_ctx.TryJump) { _ctx.RequestState(StateRequestType.Jump); return; }
        }
    }

    private void HandleMovementRequested(ParryMovementType movement)
    {
        if (_movementCommitted) return;
        _movementCommitted = true;
        _parryResultReceived = true;

        Debug.Log($"[PlayerParryState] Parry success: {movement}");

        // Nếu enable directional jump → chuyển state
        if (_ctx.ParryController.Config.enableDirectionalJump && movement != ParryMovementType.None)
        {
            _ctx.RequestParryMovement(movement);
            _ctx.RequestState(StateRequestType.ParryMovement);
        }
    }

    private void HandleParryFail()
    {
        if (_parryResultReceived) return;
        _parryResultReceived = true;   // ← IsFinished → true
        Debug.Log("[PlayerParryState] Parry failed → exit");
    }

    protected override void ExitState()
    {
        _ctx.SetAttackLock(false);
        _ctx.UnregisterVelocityProvider(this);
        _ctx.SetVelocity(0f, _ctx.JumpVelocity, 0f);

        if (_ctx.Animator != null)
        {
            _ctx.Animator.speed = 1f;
            _ctx.Animator.SetLayerWeight(PlayerController.Layer_Parry, 0f);
        }

        var pc = _ctx.ParryController;
        if (pc != null)
        {
            pc.OnParryMovementRequested -= HandleMovementRequested;
            pc.OnParryFail -= HandleParryFail;
        }

        Debug.Log("[PlayerParryState] Exit");
    }
}