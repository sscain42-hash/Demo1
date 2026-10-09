using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Object test Parry — gắn vào hộp hàng, thùng, cột...
/// 
/// SETUP:
/// 1. Gắn script này vào GameObject có Collider (Is Trigger ✅)
/// 2. Gắn thêm component TriggerDetection vào cùng object
/// 3. Trong TriggerDetection:
///    - Layer To Check = Player
///    - Collision Enter Event → trỏ tới ParryTestObject.CauseDMG(GameObject)
/// 4. Set Tag của object = "Enemy"
/// 5. Xong
/// </summary>
public class ParryTestObject : Damageable
{
    [Header("Damage")]
    [SerializeField] private int damageAmount = 100;

    [Header("Debug")]
    [SerializeField] private bool debugLog = true;

    [Header("Events")]
    [Tooltip("Gọi khi object gây damage thành công (Player không parry)")]
    public UnityEvent<GameObject> OnDamageDealt;

    [Tooltip("Gọi khi object bị Player parry. Param = Player đã parry")]
    public UnityEvent<GameObject> OnParried;

    // =========================================================
    // CORE
    // =========================================================
    public override void CauseDMG(GameObject target, AttackType type)
    {
        if (target == null) return;

        // 🔥 CHECK PARRY
        var parryReceiver = target.GetComponent<IParryReceiver>();
        if (parryReceiver != null && parryReceiver.TryParry(gameObject))
        {
            if (debugLog)
                Debug.Log($"[ParryTest] {name} bị PARRY bởi {target.name}!");

            // 🔥 CALLBACK — object tự xử lý khi bị parry
            HandleParried(target);
            return;
        }

        // ❌ Không parry → gây damage
        if (!DamageableData.Contains(target, out var receiver))
        {
            if (debugLog)
                Debug.LogWarning($"[ParryTest] {target.name} không có Damageable!");
            return;
        }

        receiver.TakeDMG(damageAmount, false);

        if (debugLog)
            Debug.Log($"[ParryTest] {target.name} nhận {damageAmount} damage");

        // Callback khi gây damage thành công
        OnDamageDealt?.Invoke(target);
    }

    /// <summary>
    /// Wrapper 1 param — dùng cho TriggerDetection.CollisionEnterEvent.
    /// </summary>
    public void CauseDMG(GameObject target) => CauseDMG(target, AttackType.NormalAttack);

    // =========================================================
    // PARRY HANDLER — Override hoặc mở rộng cho từng object
    // =========================================================
    /// <summary>
    /// Được gọi khi object bị parry.
    /// Override hàm này trong subclass nếu muốn xử lý riêng.
    /// Hoặc bind vào OnParried event trong Inspector.
    /// </summary>
    protected virtual void HandleParried(GameObject parrier)
    {
        // Gọi event cho designer bind trong Inspector
        OnParried?.Invoke(parrier);

        // TODO: mặc định có thể thêm hiệu ứng tại đây
        // Ví dụ: đổi màu, rung lắc, spawn VFX...
    }

    // =========================================================
    // TEST CALLBACKS — Bind trong Inspector
    // =========================================================
    [ContextMenu("Test/Log Parried")]
    private void LogParried(GameObject parrier)
    {
        Debug.Log($"[ParryTest] {name} bị parry bởi {parrier?.name}");
    }
}