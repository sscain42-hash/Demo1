using UnityEngine;

[CreateAssetMenu(fileName = "Ev_SpawnProjectile", menuName = "Combo System/Events/Spawn Projectile")]
public class Ev_SpawnProjectile : AnimationEventEffect
{
    [Header("📦 PROJECTILE CONFIG (CẤU HÌNH ĐẠN)")]
    [Tooltip("Kéo thoải mái GameObject Prefab viên đạn thông thường vào đây")]
    [SerializeField] private GameObject projectilePrefab;

    [Header("🚀 SPEED & LIFETIME")]
    [SerializeField] private float projectileSpeed = 15f;
    [SerializeField] private float lifeTime = 3f;

    public override void Trigger(GameObject caster,ActionWindow actionWindow)
    {
        CharacterEffect effectManager = caster.GetComponent<CharacterEffect>();
        PlayerActionComboManager comboManager = caster.GetComponent<PlayerActionComboManager>();

        if (effectManager == null || comboManager == null || comboManager.CurrentAttackData == null) return;

        GameObject prefabFromData = projectilePrefab;
        AttackType currentType = comboManager.CurrentRuntimeAttackType;

        if (prefabFromData == null)
        {
            Debug.LogWarning($"Event '{this.name}' trên {caster.name} chưa được kéo Prefab viên đạn vào!");
            return;
        }

        Vector3 spawnPosition = caster.transform.position + caster.transform.forward + (Vector3.up * 1.2f);
        Quaternion spawnRotation = caster.transform.rotation;

        // 🔥 XỬ LÝ TRƯỚC KHI GỬI VÀO HÀM SPAWN:
        Reference referenceToSpawn = prefabFromData.GetComponent<Reference>();

        // Nếu Prefab gốc chưa có sẵn Component Reference, ta tự tạo một bản mẫu ảo và AddComponent vào nó
        if (referenceToSpawn == null)
        {
            // Tạo một Instance tạm thời (chỉ tồn tại trong bộ nhớ Runtime ẩn) để gán Component
            GameObject runtimeTemplate = Instantiate(prefabFromData);
            runtimeTemplate.name = $"[Template] {prefabFromData.name}";
            runtimeTemplate.SetActive(false); // Ẩn đi để không hiển thị thừa trên Scene

            referenceToSpawn = runtimeTemplate.AddComponent<Reference>();
        }

        // 🔥 ĐÃ HẾT LỖI: Bây giờ biến "referenceToSpawn" đã mang kiểu dữ liệu 'Reference' chuẩn xác
        // Truyền thẳng nó vào hàm của bạn mà không bị báo lỗi ép kiểu Argument 1 nữa
        Reference spawnedEffect = effectManager.SpawnProjectileFromData(referenceToSpawn, spawnPosition, spawnRotation, currentType) as Reference;

        if (spawnedEffect != null)
        {
            // Lấy bộ não điều khiển ProjectileController từ viên đạn đã được sinh ra để nạp dữ liệu bay
            ProjectileController controller = spawnedEffect.GetComponent<ProjectileController>();
            if (controller != null)
            {
                // Kích hoạt nạp hướng bắn, tốc độ và thời gian sống
                controller.Initialize(caster.transform.forward, projectileSpeed, lifeTime);
            }
            else
            {
                Debug.LogWarning($"Prefab đạn '{spawnedEffect.name}' đang thiếu script ProjectileController!");
            }
        }
    }
}