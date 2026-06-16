using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;

[Category("Enemy Combat")]
[Description("Kích hoạt và duy trì chuỗi combo tấn công dựa trên thời gian cửa sổ (Windows)")]
public class AttackTargetAction : ActionTask
{
    [RequiredField]
    public BBParameter<GameObject> target; // Lấy vị trí Player từ Blackboard
    public BBParameter<float> attackRange = 2.0f; // Tầm đánh lấy từ Blackboard hoặc nhập tay

    private EnemyAttack _enemyAttack;

    // Hàm khởi tạo, gọi 1 lần duy nhất khi Node được nạp
    protected override string OnInit()
    {
        _enemyAttack = agent.GetComponent<EnemyAttack>();
        if (_enemyAttack == null)
        {
            return "Không tìm thấy Component EnemyAttack trên Agent!";
        }
        return null; // Thành công
    }

    // Gọi khi Behavior Tree bước vào Node này (Bắt đầu đòn đánh)
    protected override void OnExecute()
    {
        if (target.value == null)
        {
            EndAction(false); // Thất bại nếu không có mục tiêu
            return;
        }

        // Kích hoạt phát động đòn đầu tiên trong Combo
        _enemyAttack.EnterAttackState();
    }

    // Chạy liên tục mỗi khung hình (Tương tự hàm Update) khi Node đang hoạt động
    protected override void OnUpdate()
    {
        if (target.value == null)
        {
            _enemyAttack.ExitAttackState();
            EndAction(false);
            return;
        }

        // Cập nhật trạng thái quét Window Combo liên tục
        Transform playerTransform = target.value.transform;
        bool isStillAttacking = _enemyAttack.UpdateAttackState(playerTransform, attackRange.value);

        // Nếu UpdateAttackState trả về false (Chuỗi đòn đã đứt do hết time hoặc quá xa)
        if (!isStillAttacking)
        {
            // Kết thúc ActionTask thành công để Behavior Tree chuyển sang Node tiếp theo (ví dụ: Chạy đuổi theo, di chuyển...)
            EndAction(true);
        }
    }

    // Gọi khi Node bị ngắt ép buộc (Ví dụ: Đang đánh mà Enemy bị choáng/dính đòn từ Player)
    protected override void OnStop()
    {
        if (_enemyAttack != null)
        {
            // Thay vì gọi ExitAttackState() làm reset sạch combo, 
            // chỉ tắt trạng thái tấn công tạm thời nếu cần, hoặc giữ nguyên index.
            _enemyAttack.ExitAttackState();
        }
    }
}