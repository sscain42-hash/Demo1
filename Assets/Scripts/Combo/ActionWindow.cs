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

    [Header("Movement Step (Mặc định)")]
    [Tooltip("Quãng đường muốn di chuyển tịnh tiến tuyến tính theo thời gian")]
    public Vector3 targetDistance;

    // 🔥 THÊM CẤU HÌNH LUNGE THÔNG MINH TẠI ĐÂY:
    [Header("== Smart Lunge Settings ==")]
    [Tooltip("Bật tính năng lao tới giữ khoảng cách với mục tiêu trước mặt")]
    public bool enableLunge;
    [Tooltip("Tốc độ lao (m/s)")]
    public float lungeSpeed = 12f;
    [Tooltip("Khoảng cách lao tối đa (mét)")]
    public float maxLungeDistance = 4f;
    [Tooltip("Khoảng cách an toàn cần giữ lại với quái (không cho đâm xuyên qua)")]
    public float keepDistanceOffset = 1.2f;

    [SerializeField] public List<AnimationEventEffect> eventEffects = new List<AnimationEventEffect>();

    // Các cờ hiệu Runtime
    [HideInInspector] public bool eventTriggered;

    // 🎯 Biến Runtime để lưu vết trạng thái Lunge của riêng Window này
    [HideInInspector] public bool isLungeInitialized;
    [HideInInspector] public float actualLungeDistanceLeft;
    [HideInInspector] public Vector3 lungeDirection;

    [HideInInspector] public Vector3 calculatedTargetPos; // 🔥 Thêm dòng này để lưu vị trí Target cố định

    public void ResetRuntime()
    {
        eventTriggered = false;
        isLungeInitialized = false;
        actualLungeDistanceLeft = 0f;
        lungeDirection = Vector3.forward;
    }

    public bool IsInside(float normalizedTime)
        => normalizedTime >= startTime && normalizedTime <= endTime;
}