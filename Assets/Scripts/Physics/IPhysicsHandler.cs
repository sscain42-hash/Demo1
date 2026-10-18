using UnityEngine;

/// <summary>
/// Single Responsibility: Handle vertical physics (gravity, falling)
/// </summary>
public interface IPhysicsHandler
{
    void ApplyGravity(ref Vector3 velocity, bool isDashing);
    void ApplyGroundSnap(ref Vector3 velocity);
}
