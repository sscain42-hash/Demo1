using UnityEngine;

public static class BezierHelper
{
    /// <summary>
    /// Công thức toán học Cubic Bézier 1D/3D: B(t) = (1-t)³P0 + 3(1-t)²tP1 + 3(1-t)t²P2 + t³P3
    /// </summary>
    public static Vector3 GetCubicPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        t = Mathf.Clamp01(t);
        float u = 1f - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;

        Vector3 p = uuu * p0;
        p += 3f * uu * t * p1;
        p += 3f * u * tt * p2;
        p += ttt * p3;

        return p;
    }

    /// <summary>
    /// Tính toán 2 điểm Handle điều khiển đường cong Bézier dựa trên quỹ đạo vung kiếm
    /// </summary>
    public static void CalculateArcHandles(
        Vector3 startPos, Quaternion startRot,
        Vector3 endPos, Quaternion endRot,
        out Vector3 handle1, out Vector3 handle2)
    {
        // Hướng lưỡi chém (Trục X/Right)
        Vector3 startTangentialDir = startRot * Vector3.right;
        Vector3 endTangentialDir = endRot * Vector3.right;

        // Độ dài handle bằng một nửa khoảng cách vung kiếm để đường cong tiệm cận hình tròn (Arc)
        float handleLength = Vector3.Distance(startPos, endPos) * 0.5f;

        handle1 = startPos + startTangentialDir * handleLength;
        handle2 = endPos - endTangentialDir * handleLength;
    }
}