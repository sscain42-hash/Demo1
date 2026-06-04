// Một lớp "cái túi" chứa thông tin sự kiện
using UnityEngine;


public abstract class AnimationEventEffect : ScriptableObject
{
    // Interface cực kỳ linh hoạt, chỉ cần nhận Context
    public abstract void Trigger(GameObject caster, ActionWindow sourceWindow = null);
}