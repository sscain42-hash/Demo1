using UnityEngine;

/// <summary>
/// Encapsulate animation selection logic
/// </summary>
public class MovementAnimationHandler : IAnimationHandler
{
    private readonly Animator _animator;
    private readonly int _anim_Idle;
    private readonly int _anim_Run_F;
    private readonly int _anim_Run_B;
    private readonly int _anim_Run_L;
    private readonly int _anim_Run_R;
    private readonly int _anim_Run_FL;
    private readonly int _anim_Run_FR;
    private readonly int _anim_Run_BL;
    private readonly int _anim_Run_BR;

    private int _lastPlayedHash = -1;

    public MovementAnimationHandler(
        Animator animator,
        int anim_Idle, int anim_Run_F, int anim_Run_B,
        int anim_Run_L, int anim_Run_R,
        int anim_Run_FL, int anim_Run_FR,
        int anim_Run_BL, int anim_Run_BR)
    {
        _animator = animator;
        _anim_Idle = anim_Idle;
        _anim_Run_F = anim_Run_F;
        _anim_Run_B = anim_Run_B;
        _anim_Run_L = anim_Run_L;
        _anim_Run_R = anim_Run_R;
        _anim_Run_FL = anim_Run_FL;
        _anim_Run_FR = anim_Run_FR;
        _anim_Run_BL = anim_Run_BL;
        _anim_Run_BR = anim_Run_BR;
    }

    public int GetMovementAnimation(Vector2 inputVector, bool isAttacking)
    {
        if (inputVector.sqrMagnitude < 0.01f) return _anim_Idle;

        float x = inputVector.x;
        float y = inputVector.y;

        if (isAttacking)
        {
            if (y < -0.1f)
            {
                if (x < -0.1f) return _anim_Run_BL;
                if (x > 0.1f) return _anim_Run_BR;
                return _anim_Run_B;
            }

            if (y > 0.1f)
            {
                if (x < -0.1f) return _anim_Run_FL;
                if (x > 0.1f) return _anim_Run_FR;
                return _anim_Run_F;
            }

            if (x < -0.1f) return _anim_Run_L;
            if (x > 0.1f) return _anim_Run_R;
        }

        return _anim_Run_F;
    }

    public void PlayAnimation(int animHash, float transition = 0.1f)
    {
        if (_animator == null || _lastPlayedHash == animHash) return;
        _lastPlayedHash = animHash;
        _animator.CrossFadeInFixedTime(animHash, transition);
    }

    public void PlayAnimation(string animName, float transition = 0.1f)
    {
        if (_animator == null) return;
        _animator.CrossFadeInFixedTime(animName, transition);
    }
}
