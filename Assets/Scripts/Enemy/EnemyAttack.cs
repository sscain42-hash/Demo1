using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class EnemyAttack : MonoBehaviour, IComboCharacter, IDamageProvider
{
    private EnemyController _controller;
    private NavMeshAgent _agent;
    private ComboEngine _comboEngine;

    public int CurrentComboIndex { get; private set; } = 0;
    public bool IsAttacking { get; private set; } = false;
    public ComboSequence CurrentComboSeq { get; private set; }

    private bool _comboExecutedInThisWindow = false;

    public AttackData CurrentAttackData => _comboEngine?.CurrentAttackData;
    public AttackType CurrentRuntimeAttackType => AttackType.NormalAttack;

    private void Awake()
    {
        _controller = GetComponent<EnemyController>();
        _agent = GetComponent<NavMeshAgent>();

        Animator anim = GetComponentInChildren<Animator>();
        _comboEngine = new ComboEngine(gameObject, anim, this);
    }

    public void EnterAttackState(ComboSequence targetCombo)
    {
        if (targetCombo == null || targetCombo.attacks.Count == 0) return;

        CurrentComboSeq = targetCombo;
        IsAttacking = true;
        CurrentComboIndex = 0;
        _comboExecutedInThisWindow = false;

        if (_agent != null && _agent.gameObject.activeInHierarchy)
        {
            _agent.ResetPath(); // 🔥 Xóa đường đi cũ để tránh xung đột di chuyển cũ
            _agent.velocity = Vector3.zero; // Triệt tiêu lực quán tính di chuyển cũ
            _agent.isStopped = true;
            _agent.updatePosition = false;
        }

        ExecuteComboStep();
    }

    private void ExecuteComboStep()
    {
        _comboExecutedInThisWindow = false;
        if (CurrentComboSeq != null && CurrentComboIndex < CurrentComboSeq.attacks.Count)
        {
            _comboEngine.ChangeAttackData(CurrentComboSeq.attacks[CurrentComboIndex]);
        }
    }

    private void MoveToNextComboStep()
    {
        CurrentComboIndex++;
        ExecuteComboStep();
    }

    public bool UpdateAttackState(Transform targetTransform, float attackRange,bool continuos)
    {
        if (!IsAttacking || CurrentComboSeq == null || CurrentAttackData == null)
            return false;

        // 1. Luôn luôn quay mặt về phía Target
        if (targetTransform != null)
        {
            Vector3 direction = (targetTransform.position - transform.position).normalized;
            direction.y = 0;
            if (direction != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(direction);
        }

        // 2. CẬP NHẬT CỬA SỔ WINDOWS TRƯỚC: Phải update để lấy dữ liệu liên tục
        _comboEngine.UpdateWindows();

        // 3. 🔥 KHÓA CHUYỂN ĐỔI: Nếu Animator đang chuyển đòn (Transition), 
        // giữ nguyên trạng thái tấn công và bỏ qua các logic check Exit ở dưới.
        if (_controller.Animator.IsInTransition(0))
            return true;

        // 🔥 KIỂM TRA AN TOÀN: Đảm bảo Animator đã thực sự chuyển sang đúng tên đòn hiện tại
        AnimatorStateInfo stateInfo = _controller.Animator.GetCurrentAnimatorStateInfo(0);
        if (!stateInfo.IsName(CurrentAttackData.animationName))
            return true; // Chờ cho đến khi tên hoạt ảnh khớp hoàn toàn mới xử lý tiếp

        float normalizedTime = _comboEngine.GetNormalizedTime();
        bool isLastAttackNode = (CurrentComboIndex >= CurrentComboSeq.attacks.Count - 1);

        // 4. LOGIC XỬ LÝ CHUYỂN NHỊP COMBO (Chỉ xử lý khi CHƯA PHẢI đòn cuối)
        if (!isLastAttackNode && _comboEngine.IsComboWindowActive && !_comboExecutedInThisWindow && targetTransform != null)
        {
            float distance = Vector3.Distance(targetTransform.position, transform.position);
            float maxComboBufferRange = attackRange + 1.2f;

            if (distance <= maxComboBufferRange)
            {
                _comboExecutedInThisWindow = true;
                MoveToNextComboStep();
                return true; // 🔥 Bỏ qua luôn đoạn check Exit ở dưới vì đã sang đòn mới!
            }
        }

        // 5. Áp dụng lực tịnh tiến của đòn đánh
        if (_comboEngine.CurrentStepVelocity != Vector3.zero)
        {
            transform.position += _comboEngine.CurrentStepVelocity;
            if (_agent != null && _agent.gameObject.activeInHierarchy)
                _agent.nextPosition = transform.position;
        }

        // 6. 🔥 LOGIC CHECK EXIT (KẾT THÚC CHIÊU) CỰC KỲ MINH BẠCH
        if (isLastAttackNode)
        {
            if (continuos)
            {
                if (normalizedTime >= 0.95f)
                {
                    ExitAttackState();
                    return false;
                }
            }
            else
            {
                var bufferWindow = CurrentAttackData.windows.Find(w => w.actionName == "ComboInputBuffer");
                if (bufferWindow != null && normalizedTime >= bufferWindow.startTime)
                {
                    ExitAttackState();
                    return false;
                }
                else if (normalizedTime >= 0.95f)
                {
                    ExitAttackState();
                    return false;
                }
            }
        }
        else
        {
            // Nếu KHÔNG bấm nối đòn và đòn cũ đã chạy hết sạch hoạt ảnh (>95%) mà không có đòn mới
            if (normalizedTime >= 0.95f)
            {
                ExitAttackState();
                return false;
            }
        }

        return true;
    }
    public void ExitAttackState()
    {
        if (!IsAttacking) return;

        IsAttacking = false;
        CurrentComboIndex = 0;
        _comboExecutedInThisWindow = false;
        CurrentComboSeq = null;
        _comboEngine.ChangeAttackData(null);

        if (_agent != null && _agent.gameObject.activeInHierarchy)
        {
            _agent.updatePosition = true;
            _agent.isStopped = false;
            _agent.velocity = Vector3.zero; // Đảm bảo đứng yên sau khi kết thúc đòn
        }

        if (_controller != null && _controller.Animator != null)
        {
            _controller.Animator.CrossFadeInFixedTime("Idle", 0.15f, 0, 0f);
        }
    }

    public void ExecuteDamage(GameObject victim, AttackType attackType)
    {
        _controller.CauseDMG(victim, attackType);
    }
}