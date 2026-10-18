using UnityEngine;

/// <summary>
/// Responsive deceleration using normalized curve function
/// Release curve: v = (1-t)^2 (starts fast, slows down)
/// </summary>
public class ResponsiveDecelerationHandler
{
    private readonly float _maxSpeed;
    private readonly float _tRelease;

    public ResponsiveDecelerationHandler(float maxSpeed, float tRelease)
    {
        _maxSpeed = maxSpeed;
        _tRelease = tRelease;
    }

    /// <summary>
    /// Calculate deceleration velocity using responsive curve
    /// </summary>
    public Vector3 GetDecelerationVelocity(Vector3 currentVelocity)
    {
        float currentHorizontalSpeed = new Vector3(currentVelocity.x, 0, currentVelocity.z).magnitude;
        float normalizedSpeed = Mathf.Clamp01(currentHorizontalSpeed / _maxSpeed);

        // Calculate current t from speed using inverse release equation
        // Release: v = (1-t)^2
        // Inverse: t = 1 - sqrt(v)
        float t = 1f - Mathf.Sqrt(Mathf.Max(0f, normalizedSpeed));

        // Move forward in time
        float tNew = t + (Time.deltaTime / _tRelease);

        if (tNew >= 1f)
        {
            return Vector3.zero;
        }

        // Release curve: v = (1-t)^2
        float oneMinusT = 1f - tNew;
        float newNormalizedSpeed = oneMinusT * oneMinusT;

        float newSpeed = newNormalizedSpeed * _maxSpeed;
        
        // Preserve direction
        if (currentHorizontalSpeed > 0.01f)
        {
            Vector3 direction = new Vector3(currentVelocity.x, 0, currentVelocity.z).normalized;
            return direction * newSpeed;
        }

        return Vector3.zero;
    }
}

