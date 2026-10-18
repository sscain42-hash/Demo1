using UnityEngine;
/// <summary>
/// Đọc Animator.deltaPosition (root motion) và chuyển thành velocity.
/// Dùng chung pipeline với step window — PlayerController.ApplyMovement sẽ apply.
/// </summary>
public class RootMotionVelocityProvider : IVelocityProvider
{
    public int Priority => 5;   // Thấp hơn step window (10) nhưng cao hơn default movement
    public bool IsActive { get; private set; }

    private Vector3 _pendingDelta;
    private bool _hasPendingDelta;

    /// <summary>
    /// Gọi từ OnAnimatorMove() mỗi frame — nạp delta vào provider.
    /// </summary>
    public void FeedDelta(Vector3 worldDelta)
    {
        if (worldDelta.sqrMagnitude < 0.000001f) return;

        _pendingDelta = worldDelta;
        _hasPendingDelta = true;
        IsActive = true;
    }

    /// <summary>
    /// PlayerController.ApplyMovement gọi hàm này.
    /// Trả về velocity (delta / deltaTime).
    /// </summary>
    public Vector3 GetVelocityModifier()
    {
        if (!_hasPendingDelta) return Vector3.zero;

        // Chuyển delta → velocity
        float dt = Time.deltaTime;
        Vector3 velocity = dt > 0.0001f ? (_pendingDelta / dt) : Vector3.zero;

        // Reset sau khi đọc — mỗi frame chỉ apply 1 lần
        _hasPendingDelta = false;
        IsActive = false;

        return velocity;
    }

    public void Clear()
    {
        _pendingDelta = Vector3.zero;
        _hasPendingDelta = false;
        IsActive = false;
    }
}
