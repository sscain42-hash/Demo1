using UnityEngine;

/// <summary>
/// Air movement with momentum preservation
/// </summary>
public class AirMovementHandler : IMovementHandler
{
    private readonly float _airControl;

    public AirMovementHandler(float airControl)
    {
        _airControl = airControl;
    }

    public void ApplyMovement(Vector3 moveDir, float targetSpeed, ref Vector3 currentVelocity)
    {
        // Alias for ApplyAirControl
        ApplyAirControl(moveDir, ref currentVelocity);
    }

    public void ApplyAirControl(Vector3 moveDir, ref Vector3 currentVelocity)
    {
        float currentHorizontalSpeed = new Vector3(currentVelocity.x, 0, currentVelocity.z).magnitude;
        float targetSpeed = 12f; // Should come from config

        if (currentHorizontalSpeed < targetSpeed)
        {
            float acceleration = _airControl * Time.deltaTime;
            float newSpeed = Mathf.Min(currentHorizontalSpeed + acceleration, targetSpeed);
            Vector3 newVel = moveDir * newSpeed;
            currentVelocity.x = newVel.x;
            currentVelocity.z = newVel.z;
        }
        else if (moveDir.sqrMagnitude < 0.01f)
        {
            float friction = _airControl * 0.5f * Time.deltaTime;
            float newSpeed = Mathf.Max(currentHorizontalSpeed - friction, 0f);
            Vector3 newVel = moveDir * newSpeed;
            currentVelocity.x = newVel.x;
            currentVelocity.z = newVel.z;
        }
        else
        {
            Vector3 targetVel = moveDir * currentHorizontalSpeed;
            float lerpFactor = _airControl * 0.3f * Time.deltaTime;
            Vector3 newVel = Vector3.Lerp(new Vector3(currentVelocity.x, 0, currentVelocity.z), targetVel, lerpFactor);
            currentVelocity.x = newVel.x;
            currentVelocity.z = newVel.z;
        }
    }
}
