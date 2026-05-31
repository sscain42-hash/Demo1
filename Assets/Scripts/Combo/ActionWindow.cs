using UnityEngine;

[System.Serializable]
public class ActionWindow
{
    public string actionName; // Ví dụ: "Hitbox", "ComboInput", "DashCancel"
    [Range(0, 1)] public float startTime; // Bắt đầu tại 20% animation
    [Range(0, 1)] public float endTime;   // Kết thúc tại 50% animation
  

    [Header("Step")]
    public bool useStep;
    public float stepDistance;
    public float stepSpeed;             
    public AnimationCurve stepCurve;

    [HideInInspector] public bool stepTriggered;

    public void ResetRuntime()
    {
        stepTriggered = false;
    }
    public bool IsInside(float normalizedTime)
        => normalizedTime >= startTime && normalizedTime <= endTime;
}
