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
    [Header("💥 HIT EFFECT CONFIGS")]
    // 🔥 THÊM BIẾN NÀY: Để bạn gán tay loại VFX Hit tương ứng của đòn này qua Inspector
    [SerializeField] private Reference hitVFXPrefab;
    public override void Trigger(GameObject caster, ActionWindow actionWindow)
    {
        CharacterEffect effectManager = caster.GetComponent<CharacterEffect>();
        IComboCharacter comboManager = caster.GetComponent<IComboCharacter>();

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



        var data = actionWindow.vfxTransform;
        // 🔥 ĐÃ HẾT LỖI: Bây giờ biến "referenceToSpawn" đã mang kiểu dữ liệu 'Reference' chuẩn xác
        // Truyền thẳng nó vào hàm của bạn mà không bị báo lỗi ép kiểu Argument 1 nữa
        Reference vfxInstance = effectManager.SpawnVFXFromData(referenceToSpawn, spawnPosition, spawnRotation, currentType);
        if (vfxInstance != null)
        {
            vfxInstance.transform.localScale = data.scale;

            // 2. 🔥 TIẾN HÀNH GẮN ĐOẠN CODE CỦA BẠN VÀO ĐÂY:
            // Lấy Component nhận diện va chạm (vốn dùng để xử lý sát thương) từ vfxInstance ra
            DetectionBase detection = vfxInstance.GetComponent<DetectionBase>();
            detection.layerToCheck = caster.layer.GetOpponentLayerMask();

            // Điều kiện an toàn: Phải có component quét va chạm và bạn phải có gán hitVFXPrefab trong Inspector
            if (detection != null && hitVFXPrefab != null)
            {
                // Trước khi đăng ký Listener mới, hãy xóa sạch Listener của lần reuse trước trong Pool (nếu có)
                // để tránh việc một Object tích tụ quá nhiều Lambda trùng lặp gây lag/lỗi.
                detection.PositionEnterEvent.RemoveAllListeners();

                // Đăng ký đoạn logic kiểm tra ĐIỀU KIỆN TRÚNG ĐÒN
                detection.PositionEnterEvent.AddListener((victimPos) =>
                {
                    if (victimPos == null) return;

                    // Lấy vị trí của nạn nhân tại frame trúng đòn
                    Vector3 hitPosition = victimPos;


                    // Tính góc nổ: Quay ngược lại hướng nhìn của người chém (caster) để tạo lực phản hồi trực quan
                    Quaternion hitRotation = Quaternion.LookRotation(-caster.transform.forward);

                    // 🔥 Gọi thẳng Service tĩnh toàn cục bạn vừa viết để bắn VFX Hit ra màn hình
                    GlobalVFXManager.SpawnGlobalVFX(hitVFXPrefab, hitPosition.GetRandomPosition3D(1), hitRotation);
                });
            }
         
                // Lấy bộ não điều khiển ProjectileController từ viên đạn đã được sinh ra để nạp dữ liệu bay
                ProjectileController controller = vfxInstance.GetComponent<ProjectileController>();
                if (controller != null)
                {
                    // Kích hoạt nạp hướng bắn, tốc độ và thời gian sống
                    controller.Initialize(caster.transform.forward, projectileSpeed, lifeTime);
                }
             
            
        }
    }
}