using NodeCanvas.Framework;
using UnityEngine;

public class EnemyTryParry : ActionTask<EnemyController>
{
    public BBParameter<GameObject> attacker;

    public EnemyTryParry()
    {
    }

    protected override void OnExecute()
    {
        bool success = agent.TryParry(attacker.value);
        EndAction(success);
    }
}
