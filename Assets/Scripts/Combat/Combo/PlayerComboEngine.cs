using UnityEngine;

public class PlayerComboEngine : ComboEngineBase
{
    private readonly PlayerController _player;

    public PlayerComboEngine(PlayerController player, Animator animator)
        : base(player, animator)
    {
        _player = player;

        // 🔥 Hook khi player bị parry (dù player thường không bị parry)
        OnParriedByTarget += HandleParried;
    }

    private void HandleParried(GameObject parrier)
    {
        Debug.Log($"[PlayerComboEngine] Player bị parry bởi {parrier?.name}");
        HitStopSystem.Instance.Trigger(0.1f, 0.05f);
        // Có thể thêm stagger, VFX, SFX tại đây
    }

    protected override bool SupportsParry => true;
    protected override bool SupportsDashCancel => true;
    protected override bool SupportsJumpCancel => true;

    protected override Transform GetSwordTransform() => _player.SwordTransform;

    protected override Vector3 ResolveAimDirection(ActionWindow window, Vector3 fallback)
    {
        Vector3 aim = fallback;

        bool hasMoveInput = _player.InputVector.sqrMagnitude > 0.01f;
        if (!hasMoveInput && _player.CombatDetection != null)
        {
            aim = _player.CombatDetection.GetNearestTargetDirection(
                _player.transform.position,
                fallback,
                _player.gameObject,
                flattenY: true);
        }

        return aim;
    }

    protected override void ApplyRotationForStep(Vector3 aimDirection)
    {
        if (_player.Model == null || aimDirection == Vector3.zero) return;

        float beforeY = _player.transform.position.y;
        _player.Model.rotation = Quaternion.LookRotation(aimDirection);
        float afterY = _player.transform.position.y;

        if (Mathf.Abs(afterY - beforeY) > 0.001f)
        {
            Vector3 posFixed = _player.transform.position;
            posFixed.y = beforeY;
            _player.transform.position = posFixed;
        }
    }
}
