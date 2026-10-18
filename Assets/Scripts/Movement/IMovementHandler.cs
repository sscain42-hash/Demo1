using UnityEngine;

/// <summary>
/// Single Responsibility: Xử lý movement logic (acceleration/friction)
/// Dependency Injection: States không biết chi tiết, chỉ gọi interface
/// </summary>
public interface IMovementHandler
{
    /// <summary>Apply acceleration/friction to current velocity</summary>
    void ApplyMovement(Vector3 moveDir, float targetSpeed, ref Vector3 currentVelocity);
    
    /// <summary>Apply air control (momentum preservation)</summary>
    void ApplyAirControl(Vector3 moveDir, ref Vector3 currentVelocity);
}
