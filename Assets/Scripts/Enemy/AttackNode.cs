using UnityEngine;

public class AttackNode : BTNode
{
    private EnemyBlackboard blackboard;
    private EnemyAttack attack;

    public AttackNode(EnemyBlackboard bb, EnemyAttack attack)
    {
        blackboard = bb;
        this.attack = attack;
    }

    public override State Evaluate()
    {
        // Nếu Player chết hoặc biến mất, lập tức dừng trạng thái tấn công
        if (blackboard.Player == null)
        {
            attack.ExitAttackState();
            return State.Failure;
        }

        // Nếu đây là frame đầu tiên quái áp sát mục tiêu (Tương đương việc Player chuyển State vào Attack)
        if (!attack.IsAttacking)
        {
            attack.EnterAttackState();
            return State.Running;
        }

        // Cập nhật trạng thái chuỗi đòn chém liên tục mỗi frame
        bool isPlayingCombo = attack.UpdateAttackState(blackboard.Player, blackboard.AttackRange);

        if (isPlayingCombo)
        {
            // Trả về Running để giữ cây hành vi luôn kẹt tại Node này và chém Player liên tục
            return State.Running;
        }
        else
        {
            // Chỉ trả về Success khi chuỗi combo đứt (Player chạy thoát hoặc chết)
            return State.Success;
        }
    }
}