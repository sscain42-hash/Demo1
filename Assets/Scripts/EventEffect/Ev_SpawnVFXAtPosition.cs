using Assets.VFXPACK_IMPACT_WALLCOEUR.Scripts;
using NodeCanvas.Tasks.Actions;
using System;
using UnityEngine;

[CreateAssetMenu(fileName = "Ev_SpawnVFXAtPosition", menuName = "Combo System/Events/Spawn VFX At Position")]
public class Ev_SpawnVFXAtPosition : AnimationEventEffect
{
    [Header("📦 SPAWN CONFIGS")]
    [SerializeField] private Reference vfxPrefab; // Object gây dame (Đạn/Kiếm khí/Hitbox...)
    [Header("💥 HIT EFFECT CONFIGS")]
    // 🔥 THÊM BIẾN NÀY: Để bạn gán tay loại VFX Hit tương ứng của đòn này qua Inspector
    [SerializeField] private Reference hitVFXPrefab;

    public override void Trigger(GameObject caster, ActionWindow sourceWindow)
    {
        CharacterEffect effectManager = caster.GetComponent<CharacterEffect>();
        IComboCharacter comboManager = caster.GetComponent<IComboCharacter>();

        if (effectManager == null || comboManager == null || comboManager.CurrentAttackData == null) return;
        AttackType currentType = comboManager.CurrentRuntimeAttackType;


        var data = sourceWindow.vfxTransform;

        Vector3 spawnPosition = caster.transform.position + (caster.transform.rotation * data.positionOffset);
        Quaternion spawnRotation = caster.transform.rotation * Quaternion.Euler(data.rotationOffset);

        // Gọi pooler
        var vfxInstance = effectManager.SpawnVFXFromData(vfxPrefab, spawnPosition, spawnRotation, currentType);

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
        }
    }
}
