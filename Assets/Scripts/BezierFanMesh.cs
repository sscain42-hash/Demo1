using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class BezierFanMesh : MonoBehaviour
{
    [Range(0f, 1f)]
    public float progress = 0f;

    private Mesh mesh;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock propBlock;

    private Coroutine activeSlashCoroutine;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        propBlock = new MaterialPropertyBlock();
        gameObject.SetActive(false); // Mặc định ẩn vệt chém
    }

    /// <summary>
    /// Kích hoạt vệt chém với dữ liệu từ ScriptableObject
    /// </summary>
    public void PlaySlash(SlashDataSO data, ActionWindow sourceWindow)
    {
        if (data == null) return;

        // Gán Material từ SO nếu có
        if (data.slashMaterial != null && meshRenderer != null)
        {
            meshRenderer.sharedMaterial = data.slashMaterial;
        }

        // Tạo static mesh dựa trên danh sách Control Points trong SO
        BuildMeshFromData(data);

        // Bật GameObject
        gameObject.SetActive(true);

        // Dừng Coroutine cũ nếu đòn đánh trước chưa chạy xong
        if (activeSlashCoroutine != null) StopCoroutine(activeSlashCoroutine);

        // Chạy tiến trình progress từ 0 -> 1 dựa trên sourceWindow
        activeSlashCoroutine = StartCoroutine(AnimateSlashProgress(sourceWindow));
    }

    private IEnumerator AnimateSlashProgress(ActionWindow sourceWindow)
    {
        progress = 0f;
        UpdateShaderProgress();

        if (sourceWindow != null)
        {
            // Nếu có ActionWindow, tiến trình sẽ chạy trong khoảng từ startTime -> endTime của window đó
            float windowDuration = sourceWindow.endTime - sourceWindow.startTime;

            // Bạn có thể lấy Animator từ Caster để tính thời gian chính xác theo NormalizedTime
            // Hoặc tính theo thời gian thực (Real-time):
            float elapsed = 0f;
            while (elapsed < windowDuration)
            {
                elapsed += Time.deltaTime;
                progress = Mathf.Clamp01(elapsed / windowDuration);
                UpdateShaderProgress();
                yield return null;
            }
        }
        else
        {
            // Thời gian mặc định nếu không truyền sourceWindow (ví dụ 0.2s)
            float duration = 0.2f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                progress = Mathf.Clamp01(elapsed / duration);
                UpdateShaderProgress();
                yield return null;
            }
        }

        progress = 1f;
        UpdateShaderProgress();

        // Tự động ẩn vệt chém khi hoàn tất
        gameObject.SetActive(false);
    }

    private void UpdateShaderProgress()
    {
        if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
        if (propBlock == null) propBlock = new MaterialPropertyBlock();

        meshRenderer.GetPropertyBlock(propBlock);
        propBlock.SetFloat("_Progress", progress);
        meshRenderer.SetPropertyBlock(propBlock);
    }

    private void BuildMeshFromData(SlashDataSO data)
    {
        if (data.controlPoints == null || data.controlPoints.Count < 2) return;

        if (mesh == null)
        {
            mesh = new Mesh { name = "Bezier Fan Mesh" };
            meshFilter.sharedMesh = mesh;
        }

        int totalSections = data.controlPoints.Count - 1;
        int totalSegments = totalSections * data.segmentsPerSection;

        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        for (int i = 0; i <= totalSegments; i++)
        {
            float t = (float)i / totalSegments;

            Vector3 bottom = GetSpinePointLocal(data, t);
            Vector3 top = GetTopPointLocal(data, t);

            vertices.Add(bottom);
            vertices.Add(top);

            uvs.Add(new Vector2(t, 0f));
            uvs.Add(new Vector2(t, 1f));
        }

        for (int i = 0; i < totalSegments; i++)
        {
            int bL = i * 2;
            int tL = i * 2 + 1;
            int bR = (i + 1) * 2;
            int tR = (i + 1) * 2 + 1;

            triangles.Add(bL); triangles.Add(tL); triangles.Add(bR);
            triangles.Add(bR); triangles.Add(tL); triangles.Add(tR);

            if (data.doubleSided)
            {
                triangles.Add(bR); triangles.Add(tL); triangles.Add(bL);
                triangles.Add(tR); triangles.Add(tL); triangles.Add(bR);
            }
        }

        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    private Vector3 GetSpinePointLocal(SlashDataSO data, float tGlobal)
    {
        tGlobal = Mathf.Clamp01(tGlobal);
        int totalSections = data.controlPoints.Count - 1;

        if (tGlobal >= 1f)
            return data.controlPoints[data.controlPoints.Count - 1].localPosition;

        float scaledT = tGlobal * totalSections;
        int k = Mathf.FloorToInt(scaledT);
        float tLocal = scaledT - k;

        ControlPointData pStart = data.controlPoints[k];
        ControlPointData pEnd = data.controlPoints[k + 1];

        Vector3 p0 = pStart.localPosition;
        Vector3 p3 = pEnd.localPosition;

        float dist = Vector3.Distance(p0, p3) * data.tangentMultiplier;
        Vector3 p1 = p0 + (pStart.LocalRotation * Vector3.forward * dist);
        Vector3 p2 = p3 - (pEnd.LocalRotation * Vector3.forward * dist);

        float u = 1f - tLocal;
        float tt = tLocal * tLocal;
        float uu = u * u;

        return (uu * u * p0) + (3f * uu * tLocal * p1) + (3f * u * tt * p2) + (tt * tLocal * p3);
    }

    private Vector3 GetTopPointLocal(SlashDataSO data, float tGlobal)
    {
        tGlobal = Mathf.Clamp01(tGlobal);
        int totalSections = data.controlPoints.Count - 1;

        if (tGlobal >= 1f)
        {
            ControlPointData last = data.controlPoints[data.controlPoints.Count - 1];
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