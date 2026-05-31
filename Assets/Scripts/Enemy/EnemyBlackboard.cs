using UnityEngine;

public class EnemyBlackboard
{
    public Transform Player;
    public float AttackRange ;
    public float MoveSpeed ;

    public EnemyBlackboard(Transform player, float attackRange= 2f, float moveSpeed= 3f)
    {
        Player = player;
        AttackRange = attackRange;
        MoveSpeed = moveSpeed;
    }
}