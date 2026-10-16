using UnityEngine;

public class EnemyComboEngine : ComboEngineBase
{
    private readonly EnemyController _enemy;

    public EnemyComboEngine(EnemyController enemy, Animator animator)
        : base(enemy, animator)
    {
        _enemy = enemy;
    }

    protected override bool SupportsParry => false;
    protected override bool SupportsDashCancel => false;
    protected override bool SupportsJumpCancel => false;

    protected override Transform GetSwordTransform() => _enemy.SwordTransform;

    protected override Vector3 ResolveAimDirection(ActionWindow window, Vector3 fallback)
    {
        if (_enemy.CurrentTarget != null)
        {
            Vector3 dir = _enemy.CurrentTarget.position - _enemy.transform.position;
            dir.y = 0;
            if (dir.sqrMagnitude > 0.0001f)
                return dir.normalized;
        }

        return fallback;
    }

    protected override void ApplyRotationForStep(Vector3 aimDirection)
    {
        if (aimDirection == Vector3.zero) return;
        _enemy.transform.rotation = Quaternion.LookRotation(aimDirection);
    }

    // 🔥 TELEGRAPH → ParryController của Player
    protected override void OnTelegraphStart()
    {
        var parryController = FindPlayerParryController();
        parryController?.SetTelegraphing(true);

        Debug.Log($"[EnemyComboEngine] {_enemy.name} TELEGRAPH START");
    }

    protected override void OnTelegraphEnd()
    {
        var parryController = FindPlayerParryController();
        parryController?.SetTelegraphing(false);

        Debug.Log($"[EnemyComboEngine] {_enemy.name} TELEGRAPH END");
    }

    private Combat.Parry.ParryController FindPlayerParryController()
    {
        if (_enemy.CurrentTarget == null) return null;

        // Cache lại để tránh GetComponent mỗi frame
        return _enemy.CurrentTarget.GetComponent<Combat.Parry.ParryController>();
    }
}