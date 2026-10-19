using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(9999)]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class BezierSwordSmear : MonoBehaviour
{
    [Header("Weapon Anchors")]
    [SerializeField] private Transform basePoint;
    [SerializeField] private Transform tipPoint;

    [Header("Trail Settings (Like TrailRenderer)")]
    [Tooltip("Thời gian tồn tại của một điểm trên vệt chém (giây)")]
    [SerializeField] private float time = 0.2f;

    [Tooltip("Khoảng cách tối thiểu để ghi nhận một điểm mới (sẽ tự scale theo speed)")]
    [SerializeField] private float minVertexDistance = 0.02f;

    [Range(1, 20)][SerializeField] private int subdivisionsPerSegment = 5;
    [SerializeField] private float tipExtension = 0.3f;

    [Tooltip("Thời gian mờ dần khi dừng vung kiếm")]
    [SerializeField] private float fadeDuration = 0.15f;

    [Header("Speed Adaptation")]
    [SerializeField] private float speedThresholdLow = 2f;
    [SerializeField] private float speedThresholdHigh = 15f;
    [SerializeField] private int maxSubSteps = 256;

    [Header("Mesh Rendering")]
    [Tooltip("Tạo cả 2 mặt — bắt buộc nếu material Cull Back")]
    [SerializeField] private bool doubleSided = true;

    [Tooltip("Đảo winding order — dùng khi mesh bị lộn mặt")]
    [SerializeField] private bool flipWinding = false;

    [Header("Material")]
    [SerializeField] private Material trailMaterial;

    [Header("Debug")]
    [SerializeField] private bool logStateWhenEmpty = false;
    [SerializeField] private bool drawGizmos = false;

    // --- RUNTIME STATE ---
    private bool _isEmitting;
    private float _fadeTimer;
    private bool _isFading;

    private Mesh _dynamicMesh;
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;

    private Vector3 _smoothedBasePos;
    private Vector3 _smoothedTipPos;
    private bool _hasSmoothedPos;
    private Vector3 _prevRawBasePos;

    public class TrailPoint
    {
        public Vector3 BasePos;
        public Vector3 TipPos;
        public float TimeAdded;
    }

    private readonly List<TrailPoint> _rawPoints = new List<TrailPoint>();
    private readonly List<TrailPoint> _smoothedPoints = new List<TrailPoint>();

    // Cache lists để tránh alloc
    private readonly List<Vector3> _vertices = new List<Vector3>();
    private readonly List<Vector3> _normals = new List<Vector3>();
    private readonly List<int> _triangles = new List<int>();
    private readonly List<Vector2> _uvs = new List<Vector2>();
    private readonly List<Color> _colors = new List<Color>();

    // =========================================================
    // LIFECYCLE
    // =========================================================
    private void Awake()
    {
        _meshFilter = GetComponent<MeshFilter>();
        _meshRenderer = GetComponent<MeshRenderer>();

        if (_meshFilter == null || _meshRenderer == null)
        {
            Debug.LogError($"[BezierSwordSmear] {name}: thiếu MeshFilter hoặc MeshRenderer!");
            enabled = false;
            return;
        }

        _dynamicMesh = new Mesh { name = "DynamicTrailMesh" };
        _dynamicMesh.MarkDynamic();
        _meshFilter.sharedMesh = _dynamicMesh;

        if (trailMaterial != null)
            _meshRenderer.sharedMaterial = trailMaterial;
        else
            Debug.LogWarning($"[BezierSwordSmear] {name}: chưa gán Trail Material!");

        // Bounds lớn để tránh frustum cull frame đầu
        _dynamicMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000f);

        _meshRenderer.enabled = false;
    }

    // =========================================================
    // PUBLIC API — GỌI TỪ ANIMATION EVENT
    // =========================================================
    public void StartSmear()
    {
        if (basePoint == null || tipPoint == null)
        {
            Debug.LogWarning($"[BezierSwordSmear] {name}: chưa gán basePoint/tipPoint!");
            return;
        }

        _isEmitting = true;
        _isFading = false;
        _fadeTimer = 0f;

        ResetState();
    }

    public void StopSmear()
    {
        if (!_isEmitting) return;

        _isEmitting = false;
        _isFading = true;
        _fadeTimer = 0f;
    }

    // =========================================================
    // UPDATE
    // =========================================================
    private void Update()
    {
        if (_isEmitting)
        {
            RecordCurrentPositions();
            CleanupOldPoints();
        }

        if (_isFading)
        {
            _fadeTimer += Time.deltaTime;
            if (_fadeTimer >= fadeDuration)
            {
                _isFading = false;
                _meshRenderer.enabled = false;
                ResetState();
                return;
            }
        }

        if (_isEmitting || _isFading)
        {
            GenerateSmoothedPoints();
            UpdateDynamicMesh();
        }
    }

    // =========================================================
    // RECORD POINTS
    // =========================================================
    private void RecordCurrentPositions()
    {
        if (basePoint == null || tipPoint == null) return;

        Vector3 rawBase = basePoint.position;
        Vector3 rawTip = tipPoint.position;

        float currentSpeed = Time.deltaTime > 0.0001f
            ? Vector3.Distance(rawBase, _prevRawBasePos) / Time.deltaTime
            : 0f;
        float speedT = Mathf.InverseLerp(speedThresholdLow, speedThresholdHigh, currentSpeed);
        float smoothFactor = Mathf.Lerp(0.4f, 1f, speedT);

        if (!_hasSmoothedPos)
        {
            _smoothedBasePos = rawBase;
            _smoothedTipPos = rawTip;
            _hasSmoothedPos = true;
        }
        else
        {
            _smoothedBasePos = Vector3.Lerp(_smoothedBasePos, rawBase, smoothFactor);
            _smoothedTipPos = Vector3.Lerp(_smoothedTipPos, rawTip, smoothFactor);
        }

        _prevRawBasePos = rawBase;

        Vector3 curBase = _smoothedBasePos;
        Vector3 curTipDir = (_smoothedTipPos - _smoothedBasePos).normalized;
        if (curTipDir == Vector3.zero)
            curTipDir = transform.parent != null ? transform.parent.forward : Vector3.forward;

        float currentBladeLength = Vector3.Distance(_smoothedBasePos, _smoothedTipPos) + tipExtension;
        Vector3 curTip = curBase + curTipDir * currentBladeLength;

        if (_rawPoints.Count == 0)
        {
            AddPoint(curBase, curTip);
            // Point "ảo" nhẹ để tránh frame đầu mesh rỗng
            Vector3 fakeDir = curTipDir * 0.01f;
            AddPoint(curBase - fakeDir, curTip - fakeDir);
            return;
        }

        TrailPoint lastPoint = _rawPoints[_rawPoints.Count - 1];
        float distMoved = Vector3.Distance(lastPoint.TipPos, curTip);

        float adaptiveMinDist = Mathf.Lerp(minVertexDistance * 0.5f, minVertexDistance, speedT);
        if (distMoved < adaptiveMinDist) return;

        int subSteps = Mathf.CeilToInt(distMoved / adaptiveMinDist);
        subSteps = Mathf.Clamp(subSteps, 1, maxSubSteps);

        Vector3 lastDir = (lastPoint.TipPos - lastPoint.BasePos).normalized;
        Vector3 curDir = (curTip - curBase).normalized;
        float lastBladeLen = Vector3.Distance(lastPoint.BasePos, lastPoint.TipPos);
        float curBladeLen = Vector3.Distance(curBase, curTip);

        for (int i = 1; i <= subSteps; i++)
        {
            float t = (float)i / subSteps;
            Vector3 interpBase = Vector3.Lerp(lastPoint.BasePos, curBase, t);
            Vector3 interpDir = Vector3.Slerp(lastDir, curDir, t).normalized;
            float interpLen = Mathf.Lerp(lastBladeLen, curBladeLen, t);
            Vector3 interpTip = interpBase + interpDir * interpLen;

            AddPoint(interpBase, interpTip);
        }
    }

    private void AddPoint(Vector3 bPos, Vector3 tPos)
    {
        _rawPoints.Add(new TrailPoint
        {
            BasePos = bPos,
            TipPos = tPos,
            TimeAdded = Time.time
        });
    }

    private void CleanupOldPoints()
    {
        float expireTime = Time.time - time;
        while (_rawPoints.Count > 0 && _rawPoints[0].TimeAdded < expireTime)
            _rawPoints.RemoveAt(0);
    }

    // =========================================================
    // SMOOTH — 🔥 FIX: sinh inclusive t=1, không seam
    // =========================================================
    private void GenerateSmoothedPoints()
    {
        _smoothedPoints.Clear();
        int count = _rawPoints.Count;

        if (count == 0) return;

        if (count == 1)
        {
            var p = _rawPoints[0];
            Vector3 delta = (p.TipPos - p.BasePos) * 0.05f;
            _smoothedPoints.Add(new TrailPoint { BasePos = p.BasePos - delta, TipPos = p.TipPos - delta });
            _smoothedPoints.Add(p);
            return;
        }

        if (count == 2)
        {
            _smoothedPoints.Add(_rawPoints[0]);
            _smoothedPoints.Add(_rawPoints[1]);
            return;
        }

        for (int i = 0; i < count - 1; i++)
        {
            TrailPoint p0 = _rawPoints[Mathf.Max(i - 1, 0)];
            TrailPoint p1 = _rawPoints[i];
            TrailPoint p2 = _rawPoints[i + 1];
            TrailPoint p3 = _rawPoints[Mathf.Min(i + 2, count - 1)];

            // 🔥 Segment đầu lấy t=0; các segment sau bỏ t=0 để tránh duplicate điểm
            int startStep = (i == 0) ? 0 : 1;

            // 🔥 step <= subdivisions (inclusive) — sinh điểm cuối t=1, khớp với segment sau
            for (int step = startStep; step <= subdivisionsPerSegment; step++)
            {
                float t = (float)step / subdivisionsPerSegment;

                Vector3 smoothBase = CatmullRomHelper.GetPoint(p0.BasePos, p1.BasePos, p2.BasePos, p3.BasePos, t);
                Vector3 smoothTipRaw = CatmullRomHelper.GetPoint(p0.TipPos, p1.TipPos, p2.TipPos, p3.TipPos, t);

                Vector3 dir = (smoothTipRaw - smoothBase).normalized;
                if (dir == Vector3.zero) dir = Vector3.forward;

                float bladeLen = Vector3.Distance(p1.BasePos, p1.TipPos);
                Vector3 smoothTip = smoothBase + dir * bladeLen;

                _smoothedPoints.Add(new TrailPoint { BasePos = smoothBase, TipPos = smoothTip });
            }
        }
    }

    // =========================================================
    // MESH BUILD
    // =========================================================
    private void UpdateDynamicMesh()
    {
        if (_smoothedPoints.Count < 2)
        {
            _dynamicMesh.Clear();
            _dynamicMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000f);
            _meshRenderer.enabled = false;

            if (logStateWhenEmpty)
                Debug.Log($"[Smear] Empty: raw={_rawPoints.Count} smoothed={_smoothedPoints.Count} " +
                          $"emitting={_isEmitting} fading={_isFading} timescale={Time.timeScale:F2}");
            return;
        }

        _meshRenderer.enabled = true;

        _dynamicMesh.Clear();
        _vertices.Clear();
        _normals.Clear();
        _triangles.Clear();
        _uvs.Clear();
        _colors.Clear();

        Matrix4x4 worldToLocal = transform.worldToLocalMatrix;

        int total = _smoothedPoints.Count;
        float maxFade = _isFading ? (1f - (_fadeTimer / fadeDuration)) : 1f;

        for (int i = 0; i < total; i++)
        {
            _vertices.Add(worldToLocal.MultiplyPoint3x4(_smoothedPoints[i].BasePos));
            _vertices.Add(worldToLocal.MultiplyPoint3x4(_smoothedPoints[i].TipPos));

            float progress = (float)i / (total - 1);
            _uvs.Add(new Vector2(progress, 0));
            _uvs.Add(new Vector2(progress, 1));

            float alphaProgress = Mathf.Sin(progress * Mathf.PI);
            float finalAlpha = alphaProgress * maxFade;

            _colors.Add(new Color(1f, 1f, 1f, finalAlpha * 0.1f));
            _colors.Add(new Color(1f, 1f, 1f, finalAlpha * 1.0f));

            // 🔥 Normal thủ công — smooth dọc ribbon, không bị gộp lung tung
            Vector3 nWS = ComputeSmoothNormal(i);
            Vector3 nLocal = transform.InverseTransformDirection(nWS);
            _normals.Add(nLocal);   // base
            _normals.Add(nLocal);   // tip
        }

        GenerateTriangles();

        _dynamicMesh.SetVertices(_vertices);
        _dynamicMesh.SetNormals(_normals);   // 🔥 set normal thủ công
        _dynamicMesh.SetTriangles(_triangles, 0);
        _dynamicMesh.SetUVs(0, _uvs);
        _dynamicMesh.SetColors(_colors);
        // KHÔNG gọi RecalculateNormals — normal đã set thủ công
        _dynamicMesh.RecalculateBounds();
    }

    /// <summary>
    /// 🔥 Tính normal smooth dọc ribbon — vuông góc với mặt phẳng ribbon.
    /// Dùng central difference: lấy tangent dọc + hướng blade → cross.
    /// </summary>
    private Vector3 ComputeSmoothNormal(int index)
    {
        var p = _smoothedPoints[index];

        Vector3 along;
        if (index == 0)
            along = (_smoothedPoints[1].BasePos - p.BasePos);
        else if (index == _smoothedPoints.Count - 1)
            along = (p.BasePos - _smoothedPoints[index - 1].BasePos);
        else
            along = (_smoothedPoints[index + 1].BasePos - _smoothedPoints[index - 1].BasePos);

        Vector3 blade = (p.TipPos - p.BasePos);

        if (along.sqrMagnitude < 1e-6f || blade.sqrMagnitude < 1e-6f)
            return Vector3.up;

        Vector3 n = Vector3.Cross(blade, along).normalized;
        return n.sqrMagnitude > 1e-6f ? n : Vector3.up;
    }

    private void GenerateTriangles()
    {
        int count = _smoothedPoints.Count;

        for (int i = 0; i < count - 1; i++)
        {
            int bL = i * 2;
            int tL = i * 2 + 1;
            int bR = (i + 1) * 2;
            int tR = (i + 1) * 2 + 1;

            if (!flipWinding)
            {
                // Front face CW
                _triangles.Add(bL);
                _triangles.Add(tL);
                _triangles.Add(bR);

                _triangles.Add(bR);
                _triangles.Add(tL);
                _triangles.Add(tR);

                // Back face CCW
                if (doubleSided)
                {
                    _triangles.Add(bL);
                    _triangles.Add(bR);
                    _triangles.Add(tL);

                    _triangles.Add(bR);
                    _triangles.Add(tR);
                    _triangles.Add(tL);
                }
            }
            else
            {
                _triangles.Add(bL);
                _triangles.Add(bR);
                _triangles.Add(tL);

                _triangles.Add(bR);
                _triangles.Add(tR);
                _triangles.Add(tL);

                if (doubleSided)
                {
                    _triangles.Add(bL);
                    _triangles.Add(tL);
                    _triangles.Add(bR);

                    _triangles.Add(bR);
                    _triangles.Add(tL);
                    _triangles.Add(tR);
                }
            }
        }
    }

    // =========================================================
    // RESET
    // =========================================================
    private void ResetState()
    {
        _rawPoints.Clear();
        _smoothedPoints.Clear();
        _hasSmoothedPos = false;

        if (_dynamicMesh != null)
        {
            _dynamicMesh.Clear();
            _dynamicMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000f);
        }
    }

    // =========================================================
    // DEBUG GIZMOS
    // =========================================================
    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;
        if (_smoothedPoints == null || _smoothedPoints.Count < 2) return;

        Gizmos.color = Color.red;
        for (int i = 0; i < _smoothedPoints.Count - 1; i++)
        {
            var p1 = _smoothedPoints[i];
            var p2 = _smoothedPoints[i + 1];
            Gizmos.DrawLine(p1.BasePos, p1.TipPos);
            Gizmos.DrawLine(p2.BasePos, p2.TipPos);
            Gizmos.DrawLine(p1.BasePos, p2.BasePos);
        }
    }
}