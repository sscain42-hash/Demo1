using System.Collections.Generic;
using UnityEngine;

public class SwordHitboxManager : MonoBehaviour
{
    [Header("Cấu hình Hitbox Tùy chỉnh")]
    [Tooltip("Danh sách các vùng Hitbox (Box) do bạn định nghĩa thủ công trong Window/Inspector")]
    public List<Bounds> customHitboxes = new List<Bounds>();

    [Header("Cài đặt Kiểm tra Va chạm")]
    public LayerMask targetLayer;
    public Transform swordTransform; // Vị trí gốc của kiếm (thường là vị trí tay cầm hoặc gốc kiếm)

    [Header("Debug Visualizer")]
    public Color debugColor = new Color(1f, 0f, 0f, 0.4f);
    public bool showDebugGizmos = true;

    // Hàm gọi khi thực hiện đòn đánh để kiểm tra trúng mục tiêu
    public List<Collider> CheckHits()
    {
        List<Collider> hitTargets = new List<Collider>();
        HashSet<Collider> uniqueHits = new HashSet<Collider>();

        if (swordTransform == null)
            swordTransform = transform;

        foreach (Bounds box in customHitboxes)
        {
            // Chuyển đổi vị trí và kích thước từ không gian local của kiếm sang thế giới thực (World Space)
            Vector3 worldCenter = swordTransform.TransformPoint(box.center);
            Vector3 worldHalfExtents = Vector3.Scale(box.extents, swordTransform.lossyScale);
            Quaternion worldRotation = swordTransform.rotation;

            // Kiểm tra va chạm dạng hộp xoay theo hướng kiếm
            Collider[] hits = Physics.OverlapBox(worldCenter, worldHalfExtents, worldRotation, targetLayer);

            foreach (Collider hit in hits)
            {
                // Tránh lặp lại mục tiêu nếu nằm trong nhiều hitbox cùng lúc
                if (uniqueHits.Add(hit))
                {
                    hitTargets.Add(hit);
                }
            }
        }

        return hitTargets;
    }

    // Hiển thị trực quan các hitbox trong cửa sổ Scene
    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos) return;

        Transform currentTransform = swordTransform != null ? swordTransform : transform;
        Gizmos.color = debugColor;

        foreach (Bounds box in customHitboxes)
        {
            // Ma trận xoay và dịch chuyển theo kiếm
            Matrix4x4 rotationMatrix = Matrix4x4.TRS(
                currentTransform.position,
                currentTransform.rotation,
                currentTransform.lossyScale
            );

            Gizmos.matrix = rotationMatrix;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}