using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;

namespace AMG.BT_Nodes
{
    [Category("Combat")]
    [Description("Kích hoạt chuỗi Combo thuần túy (Không Input Buffer). Chỉ trả về Thất bại (Fail) khi ComboSequence đã thực hiện đến đòn cuối cùng.")]
    public class ExecuteComboAction : ActionTask<EnemyController>
    {
        public AttackType attackType; // NormalAttack, E, Q
        public BBParameter<ComboSequence> targetComboSequence;
        public BBParameter<float> attackRange = 2f;
        public bool continuous = false;
        public BBParameter<int> castTime;
        public BBParameter<Transform> _targetTransform;

        private EnemyAttack _enemyAttack;
        private PetController _petController;
        private bool _isComboActive;
        private int _totalAttacksInSequence;

        protected override string info => $"Execute Combo Pure: {attackType}";

        protected override string OnInit()
        {
            _enemyAttack = agent.GetComponent<EnemyAttack>();
            _petController = agent.GetComponent<PetController>();
            return (_enemyAttack == null || _petController == null) ? "Thiếu EnemyAttack hoặc PetController!" : null;
        }

        protected override void OnExecute()
        {
            // Lấy trực tiếp lệnh đầu chuỗi từ PetController
            if (_petController.ConsumeCommand(attackType))
            {
                _isComboActive = true;

                // Lưu lại tổng số đòn của chuỗi này để check điều kiện đòn cuối
                if (targetComboSequence.value != null)
                {
                    _totalAttacksInSequence = targetComboSequence.value.attacks.Count;
                }
                else
                {
                    _totalAttacksInSequence = 0;
                }

                _enemyAttack.EnterAttackState(targetComboSequence.value);
            }
            else
            {
                EndAction(false);
            }
        }

        protected override void OnUpdate()
        {
            if (!_isComboActive || _enemyAttack == null) return;

            Transform target = _targetTransform != null ? _targetTransform.value : null;

            // Để EnemyAttack tự vận hành logic Combo gốc (Tự động tăng index dựa trên khoảng cách của nó)
            _enemyAttack.UpdateAttackState(target, attackRange.value,continuous);

            // 🔥 ĐIỀU KIỆN KẾT THÚC VÀ ĐÁNH GIÁ THẤT BẠI
            if (!_enemyAttack.IsAttacking)
            {
                if (!agent.Animator.IsInTransition(0) && agent.Animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"))
                {
                    _isComboActive = false;

                    // Kiểm tra xem lúc kết thúc, hệ thống đã chạm tới đòn cuối cùng chưa
                    // (CurrentComboIndex chạy từ 0 đến Count - 1)
                    bool reachedLastAttack = (_enemyAttack.CurrentComboIndex >= _totalAttacksInSequence - 1);

                    // Dọn dẹp cờ trên Blackboard của PetController
                    _petController.ClearExecutingState(attackType);

                    if (reachedLastAttack)
                    {
                        // 🔥 YÊU CẦU: Trả về FAIL khi combo đã thực hiện đến đòn cuối cùng
                        EndAction(false);
                    }
                    else
                    {
                        // Nếu gãy giữa chừng (đòn 1, đòn 2) mà chưa tới đòn cuối -> Trả về Success
                        if (!castTime.isNone) castTime.value += 1;
                        EndAction(true);
                    }
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
                if (_petController != null)
                {
                    _petController.ClearExecutingState(attackType);
                }
            }
            _isComboActive = false;
        }
    }
}