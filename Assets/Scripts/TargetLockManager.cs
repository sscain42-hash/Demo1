using UnityEngine;
using System;

public class TargetLockManager : MonoBehaviour
{
    private GameObject _currentTarget;
    public GameObject CurrentTarget => _currentTarget;
    public bool IsLocked => _currentTarget != null;

    [Header("References")]
    public PhysicsDetection Detection; // Dùng DetectionBase để linh hoạt (Cone hoặc Sphere)

    [Header("Settings")]
    public bool IsLockingEnabled = false; // Trạng thái hệ thống

    public event Action<GameObject> OnTargetLocked;
    public event Action OnTargetUnlocked;

    private void Start()
    {
        // Đảm bảo mặc định tắt
        if (Detection != null) Detection.enabled = false;
    }

    private void Update()
    {
        // 1. Phím TAB để Bật/Tắt hệ thống
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            ToggleSystem();
        }

        // 2. Logic cập nhật
        if (IsLockingEnabled)
        {
            if (!IsLocked)
            {
                // Chưa khóa: Cho phép Detection hoạt động
                Detection.CheckCollision();
            }
            else
            {
                // Đã khóa: Kiểm tra mục tiêu có còn sống/tồn tại không
                if (_currentTarget == null || !_currentTarget.activeInHierarchy)
                {
                    Unlock();
                }
            }
        }
    }

    private void ToggleSystem()
    {
        IsLockingEnabled = !IsLockingEnabled;

        if (!IsLockingEnabled)
        {
            Unlock(); // Tắt hệ thống -> Unlock ngay
            Detection.enabled = false;
        }
        else
        {
            Detection.enabled = true;
        }

        Debug.Log($"System Enabled: {IsLockingEnabled}");
    }

    // Hàm này kết nối với CollisionEnterEvent của DetectionBase trong Inspector
    public void TryLock(GameObject target)
    {
        if (!IsLockingEnabled || IsLocked) return;

        _currentTarget = target;
        Detection.enabled = false; // Dừng quét khi đã bắt dính mục tiêu để tiết kiệm hiệu năng
        OnTargetLocked?.Invoke(_currentTarget);
    }

    public void Unlock()
    {
        if (_currentTarget == null) return;

        _currentTarget = null;
        if (IsLockingEnabled) Detection.enabled = true; // Bật lại quét để tìm mục tiêu mới
        OnTargetUnlocked?.Invoke();
    }
}