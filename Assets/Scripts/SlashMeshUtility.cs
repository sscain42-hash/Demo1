#if UNITY_EDITOR
using UnityEngine;

public static class SlashMeshUtility
{
    /// <summary>
    /// Thuật toán sinh Mesh vệt chém dùng chung cho cả Editor Preview và Runtime
    /// </summary>
    public static void GenerateMesh(SlashDataSO data, Mesh targetMesh, float progress)
    {
        if (targetMesh == null) return;

        if (data == null || data.controlPoints == null || data.controlPoints.Count < 2)
        {
            targetMesh.Clear();
            return;
        }

        progress = Mathf.Clamp01(progress);
        if (progress <= 0.001f)
        {
            targetMesh.Clear();
            return;
        }

        int segments = data.controlPoints.Count - 1;
        int stepsPerSegment = Mathf.Max(2, data.segmentsPerSection);
        int maxColumns = (segments * stepsPerSegment) + 1;

        // Số cột đỉnh hiển thị dựa trên progress
        int currentColumns = Mathf.Max(2, Mathf.CeilToInt(maxColumns * progress));

        Vector3[] vertices = new Vector3[currentColumns * 2];
        Vector2[] uvs = new Vector2[currentColumns * 2];
        int totalTriangles = (currentColumns - 1) * 6 * (data.doubleSided ? 2 : 1);
        int[] triangles = new int[totalTriangles];

        for (int i = 0; i < currentColumns; i++)
        {
            // Tỷ lệ t cố định trên đường cong (không nhân trùng progress)
            float t = (float)i / (maxColumns - 1);

            Vector3 bottomLocal = GetSpinePointLocal(data, t);
            Vector3 topLocal = GetTopPointLocal(data, t);

            int bIdx = i * 2;
            int tIdx = i * 2 + 1;

            vertices[bIdx] = bottomLocal;
            vertices[tIdx] = topLocal;

            // UV.x phản ánh đúng vị trí t trên toàn vệt chém
            uvs[bIdx] = new Vector2(t, 0f);
            uvs[tIdx] = new Vector2(t, 1f);
        }

        int triIdx = 0;
        for (int i = 0; i < currentColumns - 1; i++)
        {
            int b0 = i * 2;
            int t0 = i * 2 + 1;
            int b1 = (i + 1) * 2;
            int t1 = (i + 1) * 2 + 1;

            // Mặt trước (Front Face)
            triangles[triIdx++] = b0;
            triangles[triIdx++] = t0;
            triangles[triIdx++] = b1;

            triangles[triIdx++] = t0;
            triangles[triIdx++] = t1;
            triangles[triIdx++] = b1;

            // Mặt sau (Back Face)
            if (data.doubleSided)
            {
                triangles[triIdx++] = b0;
                triangles[triIdx++] = b1;
                triangles[triIdx++] = t0;

                triangles[triIdx++] = t0;
                triangles[triIdx++] = b1;
                triangles[triIdx++] = t1;
            }
        }

        targetMesh.Clear();
        targetMesh.vertices = vertices;
        targetMesh.uv = uvs;
        targetMesh.triangles = triangles;
        targetMesh.RecalculateNormals();
        targetMesh.RecalculateBounds();
    }

    public static Vector3 GetSpinePointLocal(SlashDataSO data, float tGlobal)
    {
        tGlobal = Mathf.Clamp01(tGlobal);
        int totalSections = data.controlPoints.Count - 1;

        if (tGlobal >= 1f)
            return data.controlPoints[totalSections].localPosition;

        float scaledT = tGlobal * totalSections;
        int k = Mathf.FloorToInt(scaledT);
        float tLocal = scaledT - k;

        ControlPointData pStart = data.controlPoints[k];
        ControlPointData pEnd = data.controlPoints[k + 1];

        Vector3 p0 = pStart.localPosition;
        Vector3 p3 = pEnd.localPosition;

        float dist = Vector3.Distance(p0, p3) / 3.0f;
        Vector3 p1 = p0 + (pStart.LocalRotation * Vector3.forward * dist);
        Vector3 p2 = p3 - (pEnd.LocalRotation * Vector3.forward * dist);

        float u = 1f - tLocal;
        float tt = tLocal * tLocal;
        float uu = u * u;

        return (uu * u * p0) + (3f * uu * tLocal * p1) + (3f * u * tt * p2) + (tt * tLocal * p3);
    }

    public static Vector3 GetTopPointLocal(SlashDataSO data, float tGlobal)
    {
        tGlobal = Mathf.Clamp01(tGlobal);
        int totalSections = data.controlPoints.Count - 1;

        if (tGlobal >= 1f)
        {
            ControlPointData last = data.controlPoints[totalSections];
            return last.localPosition + (last.LocalRotation * Vector3.up * last.fanHeight);
        }

        float scaledT = tGlobal * totalSections;
        int k = Mathf.FloorToInt(scaledT);
        float tLocal = scaledT - k;

        ControlPointData pStart = data.controlPoints[k];
        ControlPointData pEnd = data.controlPoints[k + 1];

        Quaternion currentRot = Quaternion.Slerp(pStart.LocalRotation, pEnd.LocalRotation, tLocal);
        float currentHeight = Mathf.Lerp(pStart.fanHeight, pEnd.fanHeight, tLocal);

        Vector3 spinePos = GetSpinePointLocal(data, tGlobal);

        return spinePos + (currentRot * Vector3.up * currentHeight);
    }
}
    #endif