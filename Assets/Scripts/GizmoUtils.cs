using UnityEngine;

public static class GizmoUtils
{
    public static void DrawCircle(Vector3 center, float radius, int segments = 40)
    {
        float angleStep = 360f / segments;
        float angle = 0f;

        Vector3 prevPoint = center + new Vector3(Mathf.Cos(0), 0, Mathf.Sin(0)) * radius;

        for (int i = 1; i <= segments; i++)
        {
            angle += angleStep;
            float rad = angle * Mathf.Deg2Rad;

            Vector3 newPoint = center + new Vector3(Mathf.Cos(rad), 0, Mathf.Sin(rad)) * radius;

            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
    }
}