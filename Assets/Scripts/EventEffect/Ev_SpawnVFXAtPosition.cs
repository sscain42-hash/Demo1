using Assets.VFXPACK_IMPACT_WALLCOEUR.Scripts;
using UnityEngine;

[CreateAssetMenu(fileName = "Ev_SpawnVFXAtPosition", menuName = "Combo System/Events/Spawn VFX At Position")]
public class Ev_SpawnVFXAtPosition : AnimationEventEffect
{
    [SerializeField] private Reference vfxPrefab; // Đổi thành kiểu T của bạn (ví dụ VFXController)



    public override void Trigger(GameObject caster, ActionWindow sourceWindow)
    {
        CharacterEffect effectManager = caster.GetComponent<CharacterEffect>();
        PlayerActionComboManager comboManager = caster.GetComponent<PlayerActionComboManager>();

        if (effectManager == null || comboManager == null || comboManager.CurrentAttackData == null) return;
        AttackType currentType = comboManager.CurrentRuntimeAttackType;


        var data = sourceWindow.vfxTransform;

        Vector3 spawnPosition = caster.transform.position + (caster.transform.rotation * data.positionOffset);
        Quaternion spawnRotation = caster.transform.rotation * Quaternion.Euler(data.rotationOffset);

        // Gọi pooler
        var vfxInstance = effectManager.SpawnProjectileFromData(vfxPrefab, spawnPosition, spawnRotation, currentType);

        if (vfxInstance != null)
        {
            vfxInstance.transform.localScale = data.scale;
        }
    }
}