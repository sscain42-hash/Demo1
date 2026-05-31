using UnityEngine;

public class MoveToPlayerNode : BTNode
{
    private EnemyBlackboard blackboard;
    private Transform enemyTransform;
    private float rotationSpeed = 10f;
    private EnemyController controller;

    // Thêm biến để lưu trạng thái hiện tại tránh gọi CrossFade liên tục mỗi frame làm lỗi hoạt ảnh
    private bool isMoving = false;

    public MoveToPlayerNode(EnemyBlackboard bb, Transform enemy)
    {
        blackboard = bb;
        enemyTransform = enemy;
        controller = enemy.GetComponent<EnemyController>();
    }

    public override State Evaluate()
    {
        if (blackboard.Player == null)
        {
            if (controller != null && isMoving)
            {
                controller.PlayAnimationCrossFade("IdleState", 0.15f);
                isMoving = false;
            }
            return State.Failure;
        }

        Vector3 toPlayer = blackboard.Player.position - enemyTransform.position;
        toPlayer.y = 0f;
        float distance = toPlayer.magnitude;

        // ================= ROTATE =================
        if (toPlayer.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(toPlayer.normalized);
            enemyTransform.rotation = Quaternion.Slerp(enemyTransform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        // ================= ATTACK RANGE =================
        if (distance <= blackboard.AttackRange)
        {
            if (controller != null && isMoving)
            {
                controller.PlayAnimationCrossFade("Idle", 0.15f);
                isMoving = false;
            }
            return State.Success;
        }

        // ================= MOVE =================
        Vector3 direction = toPlayer.normalized;
        enemyTransform.position += direction * blackboard.MoveSpeed * Time.deltaTime;

        if (controller != null && !isMoving)
        {
            controller.PlayAnimationCrossFade("Run", 0.15f);
            isMoving = true;
        }

        return State.Running;
    }
}