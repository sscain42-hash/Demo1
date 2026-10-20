using UnityEngine;
using Combat.Parry;

/// <summary>
/// Parry Movement — bay theo movement type, KHÔNG quay model.
/// Implement IExclusiveVelocityProvider — bypass gravity + mọi provider khác.
/// </summary>
public class PlayerParryMovementState : PlayerBaseState, IImplicitTransitionable, IExclusiveVelocityProvider
{
    private ParryMovementType _movementType;
    private Vector3 _movementVelocity;
    private Vector3 _currentVelocity;
    private AnimationCurve _speedCurve;

    public override StateCategory Category => StateCategory.Combat;

    private float _elapsedTime;
    private float _duration;

    public bool IsFinished => _elapsedTime >= _duration;

    public Vector3 GetVelocityModifier() => _currentVelocity;
    public bool IsActive => true;
    public int Priority => 100;

    public PlayerParryMovementState(PlayerController ctx, PlayerStateFactory factory)
        : base(ctx, factory) { }

    public override void EnterState()
    {
        _elapsedTime = 0f;
        _currentVelocity = Vector3.zero;

        _movementType = _ctx.ConsumePendingParryMovement();

        var config = _ctx.ParryController.Config;
        _duration = config.jumpDuration;
        _movementVelocity = CalculateVelocity(_movementType, config);
        _speedCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        _ctx.RegisterVelocityProvider(this);

        PlayAnimation(_movementType);
    }

    private void PlayAnimation(ParryMovementType movement)
    {
        if (_ctx.Animator == null) return;

        var sm = _ctx.GetComponent<PlayerSkillManager>();
        if (sm == null) return;

        string clipName = sm.GetParryAnimationName(movement);
        float clipLength = sm.GetParryClipLength(movement);

        int parryLayer = PlayerController.Layer_Parry;
        _ctx.Animator.SetLayerWeight(parryLayer, 1f);

        if (string.IsNullOrEmpty(clipName) || clipLength <= 0f)
        {
            Debug.LogWarning($"[PlayerParryMovementState] Không có clip cho {movement} — bỏ qua.");
            return;
        }

        _ctx.Animator.CrossFadeInFixedTime(clipName, 0.05f, parryLayer, 0f);

        // Auto-speed theo jumpDuration
        float speed = clipLength / Mathf.Max(0.01f, _duration);
        _ctx.Animator.speed = Mathf.Clamp(speed, 0.1f, 10f);
    }

    private Vector3 CalculateVelocity(ParryMovementType m, ParryConfig cfg)
    {
        Vector3 fwd = _ctx.MainCamera.forward; fwd.y = 0; fwd.Normalize();
        Vector3 rgt = _ctx.MainCamera.right; rgt.y = 0; rgt.Normalize();

        Vector3 dir;
        float speed;

        switch (m)
        {
            case ParryMovementType.Forward: dir = fwd; speed = cfg.forwardJumpSpeed; break;
            case ParryMovementType.Backward: dir = -fwd; speed = cfg.backwardJumpSpeed; break;
            case ParryMovementType.Left: dir = -rgt; speed = cfg.sideJumpSpeed; break;
            case ParryMovementType.Right: dir = rgt; speed = cfg.sideJumpSpeed; break;
            case ParryMovementType.ForwardLeft: dir = (fwd - rgt).normalized; speed = cfg.forwardJumpSpeed * 0.85f; break;
            case ParryMovementType.ForwardRight: dir = (fwd + rgt).normalized; speed = cfg.forwardJumpSpeed * 0.85f; break;
            case ParryMovementType.BackwardLeft: dir = (-fwd - rgt).normalized; speed = cfg.backwardJumpSpeed * 0.85f; break;
            case ParryMovementType.BackwardRight: dir = (-fwd + rgt).normalized; speed = cfg.backwardJumpSpeed * 0.85f; break;
            default: dir = -fwd; speed = cfg.backwardJumpSpeed; break;
        }

        Vector3 v = dir * speed;
        v.y = 0f;
        return v;
    }

    protected override void UpdateState()
    {
        _elapsedTime += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsedTime / _duration);
        float curve = _speedCurve.Evaluate(t);
        _currentVelocity = _movementVelocity * curve;
    }

    protected override void ExitState()
    {
        _ctx.UnregisterVelocityProvider(this);
        _currentVelocity = Vector3.zero;

        if (_ctx.Animator != null)
        {
            _ctx.Animator.speed = 1f;
            _ctx.Animator.SetLayerWeight(PlayerController.Layer_Parry, 0f);
        }
    }
}