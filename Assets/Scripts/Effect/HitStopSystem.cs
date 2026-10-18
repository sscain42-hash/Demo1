using System.Collections;
using UnityEngine;

public class HitStopSystem : MonoBehaviour
{
    public static HitStopSystem Instance;

    private Coroutine _hitStopCoroutine;
    private float _defaultTimeScale = 1f;
    public float multipler = 1f;

    // 🔥 Biến chống spam
    private float _lastHitStopEndTime = 0f;
    private const float MIN_INTERVAL = 0.05f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void Trigger(float duration, float timeScale = 0.05f)
    {
        // 🔥 Nếu đang trong HitStop, hoặc vừa kết thúc cách đây < 0.05s -> Bỏ qua
        if (Time.timeScale < 1f || Time.time < _lastHitStopEndTime + MIN_INTERVAL)
            return;

        if (_hitStopCoroutine != null)
        {
            StopCoroutine(_hitStopCoroutine);
        }

        _hitStopCoroutine = StartCoroutine(HitStopRoutine(duration * multipler, timeScale));
    }

    private IEnumerator HitStopRoutine(float duration, float timeScale)
    {
        Time.timeScale = timeScale;
        yield return new WaitForSecondsRealtime(duration);

        Time.timeScale = _defaultTimeScale;
        _lastHitStopEndTime = Time.time; // Ghi nhớ thời điểm kết thúc
        _hitStopCoroutine = null;
    }

    public void CancelHitStop()
    {
        if (_hitStopCoroutine != null)
        {
            StopCoroutine(_hitStopCoroutine);
            Time.timeScale = _defaultTimeScale;
            _lastHitStopEndTime = Time.time;
            _hitStopCoroutine = null;
        }
    }
}