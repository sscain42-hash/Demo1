using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class ActionWindow
{
    [SerializeField] public string actionName;
    [SerializeField, Range(0, 1)] public float startTime;
    [SerializeField, Range(0, 1)] public float endTime;

    [Header("VFX Transform Settings")]
    [SerializeField] public bool enableVFX;
    [SerializeField] public VFXTransformData vfxTransform = VFXTransformData.Default;
    [Header("Movement Step")]
    [Tooltip("Quãng đường muốn di chuyển trong window này")]
    public Vector3 targetDistance;



    [SerializeField] public List<AnimationEventEffect> eventEffects = new List<AnimationEventEffect>();

    // Các cờ hiệu Runtime
    [HideInInspector] public bool eventTriggered;

    public void ResetRuntime()
    {
        eventTriggered = false;
    }

    public bool IsInside(float normalizedTime)
        => normalizedTime >= startTime && normalizedTime <= endTime;
}
