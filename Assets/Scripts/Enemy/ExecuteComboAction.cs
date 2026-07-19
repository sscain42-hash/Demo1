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

        public BBParameter<Transform> _targetTransform;

        private EnemyAttack _enemyAttack;

        private bool _isComboActive;
        private int _totalAttacksInSequence;

        protected override string info => $"Execute Combo Pure: {attackType}";

        protected override string OnInit()
        {
            _enemyAttack = agent.GetComponent<EnemyAttack>();
            return (_enemyAttack == null) ? "Thiếu EnemyAttack!" : null;
        }

        protected override void OnExecute()
        {
            if (targetComboSequence.value == null)
            {
                EndAction(false);
                return;
            }

            // 🔥 SỬA TẠI ĐÂY: Khởi tạo dữ liệu khi bắt đầu thực hiện Action
            _isComboActive = true;

            // Giả sử ComboSequence của bạn có danh sách các đòn đánh (ví dụ: .attacks hoặc .Count)
            // Bạn hãy thay thế bằng thuộc tính đếm số đòn chính xác trong class ComboSequence của bạn nhé:
            _totalAttacksInSequence = targetComboSequence.value.attacks != null ? targetComboSequence.value.attacks.Count : 0;

            _enemyAttack.EnterAttackState(targetComboSequence.value);
        }

        protected override void OnUpdate()
        {
            // Nếu không active thì không làm gì cả
            if (!_isComboActive || _enemyAttack == null) return;

            Transform target = _targetTransform != null ? _targetTransform.value : null;

            // 🔥 Luôn cập nhật trạng thái đòn đánh để EnemyAttack xử lý chuyển đòn (Combo)
            _enemyAttack.UpdateAttackState(target);

            // 🔥 ĐIỀU KIỆN KẾT THÚC VÀ ĐÁNH GIÁ THẤT BẠI
            if (!_enemyAttack.IsAttacking)
            {
                // Kiểm tra Animator để chắc chắn đã kết thúc hẳn chuỗi và về trạng thái chờ
                if (agent.Animator != null && !agent.Animator.IsInTransition(0) && agent.Animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"))
                {
                    _isComboActive = false;

                    // Kiểm tra xem hệ thống đã thực hiện đến đòn cuối cùng chưa
                    bool reachedLastAttack = (_enemyAttack.CurrentComboIndex >= _totalAttacksInSequence - 1);

                    if (reachedLastAttack)
                    {
                        // 🔥 Trả về FAIL (false) khi combo đã thực hiện đến đòn cuối cùng theo yêu cầu của bạn
                        EndAction(false);
                    }
                    else
                    {
                        // Nếu chưa đến đòn cuối nhưng IsAttacking đã bằng false (có thể do lỗi hoặc hết thời gian)
                        // Bạn có thể cho EndAction(true) hoặc EndAction(false) tùy logic cây BT
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
            }
            _isComboActive = false;
        }
    }
}