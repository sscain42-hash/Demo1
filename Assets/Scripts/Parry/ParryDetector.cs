using Combat.Parry;
using UnityEngine;
/// <summary>
/// Component gắn lên Player để implement IParryReceiver.
/// Chuyển tiếp TryParry() sang ParryRegistry.
/// </summary>
[RequireComponent(typeof(ParryController))]
public class ParryDetector : MonoBehaviour, IParryReceiver
{
    public bool TryParry(GameObject attacker, Vector3 hitPoint)
    {
        return ParryRegistry.TryParry(gameObject, attacker, hitPoint);
    }
}
