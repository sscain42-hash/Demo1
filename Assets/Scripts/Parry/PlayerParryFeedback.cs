using UnityEngine;

public class PlayerParryFeedback : MonoBehaviour, IParryFeedback
{
    public void OnParrySuccess(GameObject attacker)
    {
        // HitStop, ScreenShake, Flash, VFX, SFX
    }

    public void OnParryFail() { }
    public void OnChainParry(int count) { }
}
