using UnityEngine;

public class LungeVelocityProvider : IVelocityProvider
{
    public int Priority => 10; // Đặt priority cao hơn movement thường
    public bool IsActive { get; private set; }

    private Transform _owner;
    private Transform _target;
    private float _speed;
    private float _stopDistance;
    private AnimationCurve _curve;
    private float _timer;
    private float _duration;

    public void StartLunge(Transform owner, Transform target, float distance, float duration, AnimationCurve curve)
    {
        _owner = owner;
        _target = target;
        _duration = duration;
        _curve = curve;
        _timer = 0f;
        IsActive = true;
    }

    public Vector3 GetVelocityModifier()
    {
        if (!IsActive || _target == null) return Vector3.zero;

        _timer += Time.deltaTime;
        float progress = Mathf.Clamp01(_timer / _duration);

        if (progress >= 1f)
        {
            IsActive = false;
            return Vector3.zero;
        }

        Vector3 dir = (_target.position - _owner.position);
        dir.y = 0; // Giữ lướt trên mặt phẳng ngang

        float speedEvaluated = _curve.Evaluate(progress);
        return dir.normalized * speedEvaluated * Time.deltaTime;
    }

    public void StopLunge() => IsActive = false;
}