using System;
using System.Collections;
using UnityEngine;

public class KnockbackComponent : MonoBehaviour, IKnockbackable
{
    private CharacterController _cc;
    private Coroutine _knockbackRoutine;

    public bool IsKnockedBack { get; private set; }

    public event Action OnKnockbackEnter;
    public event Action OnKnockbackExit;

    private void Awake() => _cc = GetComponent<CharacterController>();

    public void ApplyKnockback(Vector3 direction, float force, float duration)
    {
        // 1. Dừng Coroutine hiện tại (nếu có) để nhận lệnh mới nhất
        if (_knockbackRoutine != null)
        {
            StopCoroutine(_knockbackRoutine);
            // Nếu routine cũ bị dừng đột ngột, đảm bảo reset trạng thái
            if (IsKnockedBack) { IsKnockedBack = false; OnKnockbackExit?.Invoke(); }
        }

        // 2. Bắt đầu routine mới
        _knockbackRoutine = StartCoroutine(KnockbackRoutine(direction, force, duration));
    }

    private IEnumerator KnockbackRoutine(Vector3 direction, float force, float duration)
    {
        IsKnockedBack = true;
        OnKnockbackEnter?.Invoke();

        float elapsed = 0f;
        // Đảm bảo hướng đi không có thành phần Y (nếu bạn muốn quái chỉ trượt trên mặt đất)
        Vector3 knockbackDir = new Vector3(direction.x, 0, direction.z).normalized;

        while (elapsed < duration)
        {
            // Lực giảm dần tuyến tính
            float t = elapsed / duration;
            float currentForce = Mathf.Lerp(force, 0, t);

            // Nhân thêm Time.deltaTime để di chuyển mượt mà độc lập frame rate
            _cc.Move(knockbackDir * currentForce * Time.deltaTime);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Dọn dẹp sau khi kết thúc
        IsKnockedBack = false;
        _knockbackRoutine = null;
        OnKnockbackExit?.Invoke();
    }
}