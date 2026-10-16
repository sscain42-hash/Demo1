using UnityEngine;

public interface IParryFeedback
{
    void OnParrySuccess(GameObject attacker);
    void OnParryFail();
    void OnChainParry(int count);
}
