using UnityEngine;
/// <summary>
/// Component gắn lên Player để implement IParryReceiver.
/// Chuyển tiếp TryParry() sang ParryRegistry.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class ParryDetector : MonoBehaviour, IParryReceiver
{
    public bool TryParry(GameObject attacker)
    {
        return ParryRegistry.TryParry(gameObject, attacker);
    }
}