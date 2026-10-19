using UnityEngine;

[CreateAssetMenu(fileName = "Ev_SmartLunge", menuName = "Combo System/Events/Smart Lunge")]
public class Ev_SmartLunge : AnimationEvent
{
    [Tooltip("Log kết quả ra Console để debug")]
    public bool debugLog = true;

    public override void Trigger(GameObject caster, ActionWindow sourceWindow = null)
    {
        if (caster == null) return;

        var lunge = caster.GetComponent<SmartLungeController>();
        if (lunge == null)
        {
            Debug.LogWarning($"[Ev_SmartLunge] {caster.name} không có SmartLungeController!");
            return;
        }

        bool success = lunge.TryLunge();

        if (debugLog)
            Debug.Log($"[Ev_SmartLunge] {caster.name} lunge: {(success ? "OK" : "no target / out of range / in range")}");
    }
}
