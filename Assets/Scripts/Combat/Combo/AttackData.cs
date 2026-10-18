using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewAttackData", menuName = "Combat/Attack Data")]
public class AttackData : ScriptableObject
{
    public string animationName;
    [Header("Root Motion")]
    [Tooltip("Nếu true, animation sẽ dùng root motion để di chuyển nhân vật")]
    public bool useRootMotion = true;

    [Tooltip("Hệ số nhân root motion. 1 = giữ nguyên, 0.5 = giảm nửa")]
    [Range(0f, 2f)]
    public float rootMotionMultiplier = 1f;
    public List<ActionWindow> windows = new List<ActionWindow>();
    // Thêm vào ScriptableObject chứa dữ liệu đòn đánh của bạn
    [Header("Screen Shake")]
    public bool useScreenShake = true;
    public float shakeForce = 0.2f; // Đòn đánh thường nên để từ 0.1 - 0.3

    [Header("Camera Effect Settings")]
    public bool enableCameraZoom = false; // Tích chọn nếu chiêu này cần Zoom


}


