using UnityEngine;
using Combat.Parry;

/// <summary>
/// Parry Movement — bay theo movement type, KHÔNG quay model.
/// </summary>
public class PlayerParryMovementState : PlayerBaseState,IImplicitTransitionable
{
    private ParryMovementType _movementType;
    private Vector3 _movementVelocity;
    private AnimationCurve _speedCurve;
    public override StateCategory Category => StateCategory.Combat;
    private float _elapsedTime;
    private float _duration;

    public bool IsFinished => _elapsedTime >= _duration;

    public PlayerParryMovementState(PlayerController ctx, PlayerStateFactory factory)
        : base(ctx, factory)
    {
      
    }

    public override void EnterState()
    {
        _elapsedTime = 0f;

        _movementType = _ctx.ConsumePendingParryMovement();

        var config = _ctx.ParryController.Config;
        _duration = config.jumpDuration;
        _movementVelocity = CalculateVelocity(_movementType, config);
        _speedCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        _ctx.BeginParryMovement(_movementVelocity);

        PlayAnimation(_movementType);
    }

    private void PlayAnimation(ParryMovementType movement)
    {
        int hash = _ctx.GetParryMovementHash(movement);
        _ctx.Animator.CrossFadeInFixedTime(hash, 0.05f, 0, 0f);
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
        _ctx.UpdateParryMovementVelocity(_movementVelocity * curve);
    }

    protected override void ExitState()
    {
        _ctx.EndParryMovement();
    }
}
