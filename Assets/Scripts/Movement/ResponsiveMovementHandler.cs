using UnityEngine;

/// <summary>
/// Responsive movement based on "Building Better Movement" by Nishchal Bhandari
/// Uses normalized curve functions for attack/release instead of linear acceleration
/// </summary>
public class ResponsiveMovementHandler : IMovementHandler
{
    private readonly float _maxSpeed;
    private readonly float _tAttack;

    public ResponsiveMovementHandler(float maxSpeed, float tAttack)
    {
        _maxSpeed = maxSpeed;
        _tAttack = tAttack;
    }

    public void ApplyMovement(Vector3 moveDir, float targetSpeed, ref Vector3 currentVelocity)
    {
        // Chỉ xử lý Input di chuyển của Player
        float currentHorizontalSpeed = new Vector3(currentVelocity.x, 0, currentVelocity.z).magnitude;
        float normalizedSpeed = Mathf.Clamp01(currentHorizontalSpeed / _maxSpeed);
        float t = 1f - Mathf.Sqrt(Mathf.Max(0f, 1f - normalizedSpeed));
        float tNew = t + (Time.deltaTime / _tAttack);

        float newNormalizedSpeed = (tNew >= 1f) ? 1f : (1f - Mathf.Pow(1f - tNew, 2f));
        Vector3 newVel = moveDir * (newNormalizedSpeed * _maxSpeed);

        currentVelocity.x = newVel.x;
        currentVelocity.z = newVel.z;

        // Debug.Log($"[Input] Velocity: {currentVelocity}");
    }

    public void ApplyAirControl(Vector3 moveDir, ref Vector3 currentVelocity)
    {
        // Same as ground but with modified time constant
        float currentHorizontalSpeed = new Vector3(currentVelocity.x, 0, currentVelocity.z).magnitude;
        float normalizedSpeed = Mathf.Clamp01(currentHorizontalSpeed / _maxSpeed);

        float t = 1f - Mathf.Sqrt(Mathf.Max(0f, 1f - normalizedSpeed));
        float tNew = t + (Time.deltaTime / (_tAttack * 2f)); // Slower in air

        float newNormalizedSpeed;
        if (tNew >= 1f)
        {
            newNormalizedSpeed = 1f;
        }
        else
        {
            float oneMinusT = 1f - tNew;
            newNormalizedSpeed = 1f - (oneMinusT * oneMinusT);
        }

        float newSpeed = newNormalizedSpeed * _maxSpeed;
        Vector3 newVel = moveDir * newSpeed;
        currentVelocity.x = newVel.x;
        currentVelocity.z = newVel.z;
    }
}
