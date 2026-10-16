using UnityEngine;

public class EnemyParryFeedback : MonoBehaviour, IParryFeedback
{
    [SerializeField] private Reference parryVFXPrefab;

    public void OnParrySuccess(GameObject attacker)
    {
        // VFX nhỏ hơn Player, không screen shake
        GlobalVFXManager.SpawnGlobalVFX(parryVFXPrefab, transform.position, transform.rotation);
    }

    public void OnParryFail() { }
    public void OnChainParry(int count) { }
}