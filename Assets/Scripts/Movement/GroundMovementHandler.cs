using UnityEngine;

/// <summary>
/// Ground movement with acceleration/friction
/// </summary>
public class GroundMovementHandler : IMovementHandler
{
    private readonly float _acceleration;
    private readonly float _friction;

    public GroundMovementHandler(float acceleration, float friction)
    {
        _acceleration = acceleration;
        _friction = friction;
    }

    public void ApplyMovement(Vector3 moveDir, float targetSpeed, ref Vector3 currentVelocity)
    {
        float currentHorizontalSpeed = new Vector3(currentVelocity.x, 0, currentVelocity.z).magnitude;
        float speedDifference = targetSpeed - currentHorizontalSpeed;

        float newSpeed;
        if (speedDifference > 0)
        {
            newSpeed = Mathf.Min(currentHorizontalSpeed + _acceleration * Time.deltaTime, targetSpeed);
        }
        else if (speedDifference < 0)
        {
            newSpeed = Mathf.Max(currentHorizontalSpeed - _friction * Time.deltaTime, 0f);
        }
        else
        {
            newSpeed = currentHorizontalSpeed;
        }

        Vector3 newVel = moveDir * newSpeed;
        currentVelocity.x = newVel.x;
        currentVelocity.z = newVel.z;
    }

    public void ApplyAirControl(Vector3 moveDir, ref Vector3 currentVelocity)
    {
        // Not used in ground movement
    }
}
