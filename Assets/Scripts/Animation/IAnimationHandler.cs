using UnityEngine;

/// <summary>
/// Single Responsibility: Handle animation playback
/// </summary>
public interface IAnimationHandler
{
    int GetMovementAnimation(Vector2 inputVector, bool isAttacking);
    void PlayAnimation(int animHash, float transition = 0.1f);
    void PlayAnimation(string animName, float transition = 0.1f);
}
