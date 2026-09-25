using UnityEngine;

public static class CatmullRomHelper
{
    /// <summary>
    /// Tính vị trí trên đường cong Catmull-Rom giữa P1 và P2 theo tham số t (0 -> 1)
    /// </summary>
    public static Vector3 GetPoint(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        t = Mathf.Clamp01(t);
        float t2 = t * t;
        float t3 = t2 * t;

        Vector3 point = 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );

        return point;
    }
}