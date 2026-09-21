using Assets.VFXPACK_IMPACT_WALLCOEUR.Scripts;
using NodeCanvas.Tasks.Actions;
using UnityEngine;

[CreateAssetMenu(fileName = "Ev_SpawnVFXAtPosition", menuName = "Combo System/Events/Spawn VFX At Position")]
public class Ev_SpawnVFXAtPosition : AnimationEvent
{
    [Header("📦 SPAWN CONFIGS")]
    [SerializeField] private Reference vfxPrefab; // Object gây dame (Đạn/Kiếm khí/Hitbox...)

    [Header("💥 HIT EFFECT CONFIGS")]
    [SerializeField] private Reference hitVFXPrefab; // Major Hit VFX (Lớn, sáng)
    [SerializeField] private Reference minorHitVFXPrefab; // Minor Hit VFX (Phụ, nhỏ - Tùy chọn)

    public override void Trigger(GameObject caster, ActionWindow sourceWindow)
    {
        CharacterEffect effectManager = caster.GetComponent<CharacterEffect>();
        IComboCharacter comboManager = caster.GetComponent<IComboCharacter>();

        if (effectManager == null || comboManager == null || comboManager.CurrentAttackData == null) return;
        AttackType currentType = comboManager.CurrentRuntimeAttackType;

        var data = sourceWindow.vfxTransform;

        Vector3 spawnPosition = caster.transform.position + (caster.transform.rotation * data.positionOffset);
        Quaternion spawnRotation = caster.transform.rotation * Quaternion.Euler(data.rotationOffset);

        // Gọi pooler sinh vfx chính (Projectile/Slash Wave...)
        var vfxInstance = effectManager.SpawnVFXFromData(vfxPrefab, spawnPosition, spawnRotation, currentType);

        if (vfxInstance != null)
        {
            vfxInstance.transform.localScale = data.scale;

            DetectionBase detection = vfxInstance.GetComponent<DetectionBase>();

            if (detection != null)
            {
                detection.layerToCheck = caster.layer.GetOpponentLayerMask();

                if (hitVFXPrefab != null)
                {
                    // Cờ đánh dấu xem vfxInstance này đã va chạm mục tiêu nào chưa
                    bool hasHitPrimaryTarget = false;

                    // KHÔNG dùng RemoveAllListeners() để tránh xóa logic gốc của DetectionBase.
                    // Chỉ loại bỏ lambda cũ nếu bạn có delegate cụ thể, hoặc dùng UnityEvent thông thường.
                    detection.PositionEnterEvent.RemoveAllListeners();

                    detection.PositionEnterEvent.AddListener((victimPos) =>
                    {
                        if (victimPos == null) return;

                        Vector3 hitPosition = victimPos;

                        // Tính góc nổ: Quay ngược lại hướng mặt của Caster để tia lửa/vệt máu bắn về phía người chơi
                        Vector3 dirToCaster = (caster.transform.position - hitPosition).normalized;
                        dirToCaster.y = 0; // Giữ phẳng theo trục ngang nếu cần

                        Quaternion hitRotation = dirToCaster != Vector3.zero
                            ? Quaternion.LookRotation(dirToCaster)
                            : caster.transform.rotation;

                        // PHÂN LOẠI MAJOR VÀ MINOR VFX
                        if (!hasHitPrimaryTarget)
                        {
                            // Va chạm ĐẦU TIÊN ➔ Spawn Major Hit VFX
                            hasHitPrimaryTarget = true;
                            GlobalVFXManager.SpawnGlobalVFX(hitVFXPrefab, hitPosition, hitRotation);
                        }
                        else if (minorHitVFXPrefab != null)
                        {
                            // Các va chạm TIẾP THEO trong cùng 1 đòn ➔ Spawn Minor Hit VFX (nhẹ hơn)
                            Vector3 offsetPos = hitPosition + Random.insideUnitSphere * 0.1f;
                            GlobalVFXManager.SpawnGlobalVFX(minorHitVFXPrefab, offsetPos, hitRotation);
                        }
                    });
                }
            }
        }
    }
}