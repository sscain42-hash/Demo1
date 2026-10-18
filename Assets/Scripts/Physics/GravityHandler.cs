using UnityEngine;

/// <summary>
/// Encapsulate gravity calculations
/// </summary>
public class GravityHandler : IPhysicsHandler
{
    private readonly float _gravity;
    private readonly float _gravityScaling;
    private readonly float _fallClamp;

    public GravityHandler(float gravity, float gravityScaling, float fallClamp)
    {
        _gravity = gravity;
        _gravityScaling = gravityScaling;
        _fallClamp = fallClamp;
    }

    public void ApplyGravity(ref Vector3 velocity, bool isDashing)
    {
        if (isDashing) return;

        if (velocity.y < 0f)
        {
            float multiplier = _gravityScaling;
            velocity.y += _gravity * multiplier * Time.deltaTime;
        }
        else
        {
            velocity.y += _gravity * Time.deltaTime;
        }

        velocity.y = Mathf.Max(velocity.y, _fallClamp);
    }

    public void ApplyGroundSnap(ref Vector3 velocity)
    {
        if (velocity.y < 0f)
            velocity.y = -2f;
    }
}
