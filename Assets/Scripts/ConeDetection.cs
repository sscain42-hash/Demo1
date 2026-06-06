using System.Collections.Generic;
using UnityEngine;

public class ConeDetection : DetectionBase
{
    public float detectionRadius = 15f;
    public float viewAngle = 45f;
    public Transform sensorOrigin; // Camera hoặc Player
    public LayerMask targetLayer;

    // Lưu danh sách các object đã phát hiện từ frame trước
    private HashSet<GameObject> _lastDetectedObjects = new HashSet<GameObject>();

    private void Update()
    {
        Detect();
    }

    private void Detect()
    {
        // 1. Quét tất cả xung quanh
        Collider[] hits = Physics.OverlapSphere(sensorOrigin.position, detectionRadius, targetLayer);
        HashSet<GameObject> currentDetected = new HashSet<GameObject>();

        // 2. Lọc theo hình nón
        foreach (var hit in hits)
        {
            Vector3 dir = (hit.transform.position - sensorOrigin.position).normalized;
            if (Vector3.Angle(sensorOrigin.forward, dir) < viewAngle)
            {
                currentDetected.Add(hit.gameObject);
            }
        }

        // 3. So sánh để bắn Event
        // Kiểm tra đối tượng mới vào
        foreach (var obj in currentDetected)
        {
            if (!_lastDetectedObjects.Contains(obj))
            {
                CollisionEnterEvent?.Invoke(obj);
                PositionEnterEvent?.Invoke(obj.transform.position);
            }
        }

        // Kiểm tra đối tượng vừa rời khỏi
        foreach (var obj in _lastDetectedObjects)
        {
            if (!currentDetected.Contains(obj))
            {
                CollisionExitEvent?.Invoke(obj);
                PositionExitEvent?.Invoke(obj.transform.position);
            }
        }

        // Cập nhật danh sách cho frame kế tiếp
        _lastDetectedObjects = currentDetected;
    }

    private void OnDrawGizmosSelected()
    {
        // Vẽ vùng quét để dễ quan sát
        if (sensorOrigin == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(sensorOrigin.position, detectionRadius);
    }
}