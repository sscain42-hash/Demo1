using System.Collections;
using UnityEngine;

public class HitStopSystem : MonoBehaviour
{
    public static HitStopSystem Instance;

    [Header("Config")]
    [SerializeField] private float minTimeScale = 0.05f;
    [SerializeField] private float recoverSpeed = 8f;
    [SerializeField] private float blendSpeed = 20f;

    private float _targetTimeScale = 1f;
    private float _currentTimeScale = 1f;

    private float _hitStopTimer = 0f;

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        // 🔥 giảm timer
        if (_hitStopTimer > 0f)
        {
            _hitStopTimer -= Time.unscaledDeltaTime;

            // giữ low timescale
            _targetTimeScale = minTimeScale;
        }
        else
        {
            // 🔥 recover về 1
            _targetTimeScale = 1f;
        }

        // 🔥 blend mượt (KHÔNG giật)
        _currentTimeScale = Mathf.Lerp(
            _currentTimeScale,
            _targetTimeScale,
            Time.unscaledDeltaTime * (_targetTimeScale < 1f ? blendSpeed : recoverSpeed)
        );

        Time.timeScale = _currentTimeScale;
    }

    // ================= API =================

    public void Trigger(float duration)
    {
        // 🔥 stack hit stop (không override)
        _hitStopTimer = Mathf.Max(_hitStopTimer, duration);
    }

    public void Trigger(float duration, float customScale)
    {
        _hitStopTimer = Mathf.Max(_hitStopTimer, duration);
        minTimeScale = customScale;
    }
}