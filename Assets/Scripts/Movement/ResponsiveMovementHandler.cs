using UnityEngine;

/// <summary>
/// Momentum-based movement — gia tốc khi có input, giảm tốc khi không input.
/// Học từ Megabonk / The Pathless: chuyển động có quán tính, không snap.
/// </summary>
public class ResponsiveMovementHandler : IMovementHandler
{
    private readonly float _maxSpeed;
    private readonly float _tAttack;    // Giây để đạt max speed
    private readonly float _tRelease;   // Giây để dừng hẳn

    public ResponsiveMovementHandler(float maxSpeed, float tAttack, float tRelease)
    {
        _maxSpeed = maxSpeed;
        _tAttack = Mathf.Max(0.01f, tAttack);
        _tRelease = Mathf.Max(0.01f, tRelease);
    }

    public void ApplyMovement(Vector3 moveDir, float targetSpeed, ref Vector3 currentVelocity)
    {
        Vector3 horizontal = new Vector3(currentVelocity.x, 0f, currentVelocity.z);

        if (moveDir.sqrMagnitude > 0.001f && targetSpeed > 0.001f)
        {
            // ATTACK — tăng tốc tới target
            Vector3 targetVel = moveDir.normalized * targetSpeed;
            float accel = targetSpeed / _tAttack;   // m/s²
            Vector3 newVel = Vector3.MoveTowards(horizontal, targetVel, accel * Time.deltaTime);
            currentVelocity.x = newVel.x;
            currentVelocity.z = newVel.z;
        }
        else
        {
            // RELEASE — giảm tốc về 0
            float decel = _maxSpeed / _tRelease;    // m/s²
            Vector3 newVel = Vector3.MoveTowards(horizontal, Vector3.zero, decel * Time.deltaTime);
            currentVelocity.x = newVel.x;
            currentVelocity.z = newVel.z;
        }
    }

    public void ApplyAirControl(Vector3 moveDir, ref Vector3 currentVelocity)
    {
        // Trong không khí: gia tốc yếu hơn 3x, giữ momentum ngang
        Vector3 horizontal = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
        if (moveDir.sqrMagnitude < 0.001f) return;   // Không input → giữ nguyên momentum

        Vector3 targetVel = moveDir.normalized * _maxSpeed;
        float airAccel = _maxSpeed / (_tAttack * 3f);
        Vector3 newVel = Vector3.MoveTowards(horizontal, targetVel, airAccel * Time.deltaTime);
        currentVelocity.x = newVel.x;
        currentVelocity.z = newVel.z;
    }
}
