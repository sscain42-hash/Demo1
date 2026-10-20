using UnityEngine;

public static class ParryReceiverExtensions
{
    public static bool TryParry(this IParryReceiver r, GameObject attacker)
        => r.TryParry(attacker, attacker != null ? attacker.transform.position : Vector3.zero);
}