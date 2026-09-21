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
    [SerializeField] public TransformData vfxTransform = TransformData.Default;

    [Header("Movement Step (Mặc định)")]
    public bool cursorStep;
    public Vector3 targetDistance;

    [Header("HitBox Settings (OverlapBox)")]
    public Vector3 hitBoxSize = new Vector3(1.5f, 1.5f, 2f);
    public Vector3 hitBoxOffset = new Vector3(0f, 1f, 1.5f);
    public LayerMask targetLayer;

    [SerializeField] public List<AnimationEvent> eventEffects = new List<AnimationEvent>();

    [HideInInspector] public bool eventTriggered;

    public void ResetRuntime()
    {
        eventTriggered = false;
    }

    public bool IsInside(float normalizedTime)
        => normalizedTime >= startTime && normalizedTime <= endTime;
}