using UnityEngine;

/// <summary>
/// Provider bình thường — PlayerController pick provider Priority cao nhất khi nhiều cái active.
/// </summary>
public interface IVelocityProvider
{
    Vector3 GetVelocityModifier();
    bool IsActive { get; }
    int Priority { get; }
}

/// <summary>
/// MARKER INTERFACE — Provider "độc quyền".
/// 
/// Khi active:
///   - Bypass toàn bộ provider khác (kể cả Priority cao hơn)
///   - Bypass gravity (jumpVelocity = 0)
///   - Apply trực tiếp velocity, không qua fallback movement
/// 
/// Dùng cho: ParryMovement, SmartLunge, Knockback, GrapplingHook, 
/// Teleport, Dash đặc biệt... bất cứ thứ gì cần override hoàn toàn.
/// 
/// PlayerController KHÔNG cần biết class cụ thể — chỉ check marker này.
/// </summary>
public interface IExclusiveVelocityProvider : IVelocityProvider { }