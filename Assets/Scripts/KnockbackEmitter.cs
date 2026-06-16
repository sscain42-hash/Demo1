using UnityEngine;

public class KnockbackEmitter : MonoBehaviour
{
    [Header("Knockback Settings")]
    [SerializeField] private float force = 10f;
    [SerializeField] private float duration = 0.2f;

    // Hàm này sẽ được gọi từ TriggerDetection
    public void EmitKnockback(GameObject target)
    {
        if (target.TryGetComponent<IKnockbackable>(out var knockbackable))
        {
            // Tính hướng đẩy từ tâm người đánh đến mục tiêu
            Vector3 direction = (target.transform.position - transform.position).normalized;
            // Đảm bảo lực đẩy có hướng lên một chút nếu cần (tùy chỉnh)
            direction.y = 0.1f;

            knockbackable.ApplyKnockback(direction, force, duration);
        }
    }
}