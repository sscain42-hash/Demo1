using System.Collections;
using UnityEngine;

public class HitStopSystem : MonoBehaviour
{
    public static HitStopSystem Instance;

    private Coroutine _hitStopCoroutine;
    private float _defaultTimeScale = 1f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// Kích hoạt HitStop.
    /// </summary>
    /// <param name="duration">Thời gian dừng (tính bằng giây thực - không bị ảnh hưởng bởi timeScale)</param>
    /// <param name="timeScale">Tỉ lệ thời gian (ví dụ 0.05f)</param>
    public void Trigger(float duration, float timeScale = 0.05f)
    {
        // 1. Dừng mọi HitStop đang chạy dở để ưu tiên nhát chém mới nhất
        if (_hitStopCoroutine != null)
        {
            StopCoroutine(_hitStopCoroutine);
        }

        // 2. Chạy Coroutine mới
        _hitStopCoroutine = StartCoroutine(HitStopRoutine(duration, timeScale));
    }

    private IEnumerator HitStopRoutine(float duration, float timeScale)
    {
        // 3. Set TimeScale ngay lập tức
        Time.timeScale = timeScale;

        // 4. Đợi thời gian thực. Quan trọng: Dùng WaitForSecondsRealtime 
        // để không bị "đứng" vĩnh viễn khi timeScale = 0
        yield return new WaitForSecondsRealtime(duration);

        // 5. Phục hồi thời gian
        Time.timeScale = _defaultTimeScale;
        _hitStopCoroutine = null;
    }

    // Nếu muốn reset thủ công khi cần
    public void CancelHitStop()
    {
        if (_hitStopCoroutine != null)
        {
            StopCoroutine(_hitStopCoroutine);
            Time.timeScale = _defaultTimeScale;
            _hitStopCoroutine = null;
        }
    }
}