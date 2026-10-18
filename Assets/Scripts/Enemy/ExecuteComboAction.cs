using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;

namespace AMG.BT_Nodes
{
    [Category("Combat")]
    [Description("Chạy chuỗi combo liên tục. Luôn trả về Success khi combo xong để BT loop lại — trừ khi Player chết.")]
    public class ExecuteComboAction : ActionTask<EnemyController>
    {
 

        [RequiredField]
        public BBParameter<ComboSequence> targetComboSequence;

        [RequiredField]
        public BBParameter<Transform> _targetTransform;

        private EnemyAttack _enemyAttack;

        private bool _isComboActive;
        private int _totalAttacksInSequence;



        protected override string OnInit()
        {
            _enemyAttack = agent.GetComponent<EnemyAttack>();
            return (_enemyAttack == null) ? "Thiếu EnemyAttack!" : null;
        }

        protected override void OnExecute()
        {
            if (targetComboSequence.value == null) { EndAction(false); return; }
            if (_targetTransform.value == null) { EndAction(false); return; }

            _isComboActive = true;

          

            _enemyAttack.EnterAttackState(targetComboSequence.value);
        }

        protected override void OnUpdate()
        {
            if (!_isComboActive || _enemyAttack == null) return;

            Transform target = _targetTransform.value;

            // 🔥 Player chết hoặc mất target → fail để BT thoát khỏi nhánh này
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                _isComboActive = false;
                if (_enemyAttack.IsAttacking) _enemyAttack.ExitAttackState();
                EndAction(false);
                return;
            }

            // Cập nhật trạng thái tấn công (bao gồm xoay về target + update windows)
            _enemyAttack.UpdateAttackState(target);

            if (!_enemyAttack.IsAttacking)
            {
                // 🔥 FIX: Bỏ check IsName("Idle"), chỉ cần không transition
                if (agent.Animator != null && !agent.Animator.IsInTransition(0))
                {
                    _isComboActive = false;
                    EndAction(true);
                }
            }
        }

        protected override void OnStop()
        {
            if (_isComboActive)
            {
                if (_enemyAttack != null && _enemyAttack.IsAttacking)
                {
                    _enemyAttack.ExitAttackState();
                }
            }
            _isComboActive = false;
        }
    }
}
