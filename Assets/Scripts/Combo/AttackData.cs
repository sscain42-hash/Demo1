using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewAttackData", menuName = "Combat/Attack Data")]
public class AttackData : ScriptableObject
{
    public string animationName;
    public List<ActionWindow> windows = new List<ActionWindow>();
    // Thêm vào ScriptableObject chứa dữ liệu đòn đánh của bạn
    [Header("Screen Shake")]
    public bool useScreenShake = true;
    public float shakeForce = 0.2f; // Đòn đánh thường nên để từ 0.1 - 0.3

    [Header("Camera Effect Settings")]
    public bool enableCameraZoom = false; // Tích chọn nếu chiêu này cần Zoom

  
}
