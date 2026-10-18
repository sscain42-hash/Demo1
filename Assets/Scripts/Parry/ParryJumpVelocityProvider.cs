using UnityEngine;

/// <summary>
/// 🔥 Velocity provider cho cú nhảy sau parry.
/// Curve boost mạnh lúc đầu → cảm giác "bật" nhanh, dứt khoát.
/// </summary>
public class ParryJumpVelocityProvider : IVelocityProvider
{
    public int Priority => 15;
    public bool IsActive { get; private set; }

    private Vector3 _jumpVelocity;
    private float _timer;
    private float _duration;
    private AnimationCurve _curve;

    public void Start(Vector3 jumpVelocity, float duration)
    {
        _jumpVelocity = jumpVelocity;
        _duration = Mathf.Max(0.05f, duration);
        _timer = 0f;
        IsActive = true;

        // 🔥 Curve boost mạnh lúc đầu — "snap" 60% quãng đường trong 0.1s
        _curve = new AnimationCurve(
            new Keyframe(0f, 2.5f),      // Boost 2.5x ngay
            new Keyframe(0.15f, 1.8f),   // Vẫn còn mạnh
            new Keyframe(0.4f, 0.8f),    // Giảm dần
            new Keyframe(1f, 0f)         // Kết thúc
        );
    }

    public Vector3 GetVelocityModifier()
    {
        if (!IsActive) return Vector3.zero;

        _timer += Time.deltaTime;
        float progress = Mathf.Clamp01(_timer / _duration);

        if (progress >= 1f)
        {
            IsActive = false;
            return Vector3.zero;
        }

        float curveValue = _curve.Evaluate(progress);
        return _jumpVelocity * curveValue;
    }

    public void Stop() => IsActive = false;
}
