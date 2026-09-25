using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(9999)]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class BezierSwordSmear : MonoBehaviour
{
    [Header("Weapon Anchors")]
    [SerializeField] private Transform basePoint;
    [SerializeField] private Transform tipPoint;

    [Header("Trail Settings")]
    [Tooltip("Khoảng cách tối thiểu giữa các khung hình để ghi điểm (Càng nhỏ đường chém càng chi tiết)")]
    [SerializeField] private float minDistance = 0.02f;

    [Tooltip("Số điểm làm mượt chèn thêm giữa 2 khung hình")]
    [Range(1, 10)][SerializeField] private int subdivisionsPerSegment = 5;

    [Tooltip("Thời gian trail mờ dần sau khi chém xong (giây)")]
    [SerializeField] private float fadeDuration = 0.2f;

    [Tooltip("Phóng to bán kính vệt chém")]
    [SerializeField] private float tipExtension = 0.3f;

    [Tooltip("Thời gian tối đa 1 đòn chém (giây)")]
    [SerializeField] private float maxSwingDuration = 1.5f;

    [Tooltip("Khoảng cách Teleport tối đa của gốc kiếm (chống lỗi rác khi Cancel Animation)")]
    [SerializeField] private float maxAllowedBaseJump = 5.0f;

    [Header("Material")]
    [SerializeField] private Material trailMaterial;

    private bool _isSwinging;
    private float _swingTimer;
    private float _bladeLength;
    private Mesh _dynamicMesh;
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;

    private static readonly Queue<GameObject> FaderPool = new Queue<GameObject>();

    public class TrailPoint
    {
        public Vector3 BasePos;
        public Vector3 TipPos;
    }

    private readonly List<TrailPoint> _rawPoints = new List<TrailPoint>();
    private readonly List<TrailPoint> _smoothedPoints = new List<TrailPoint>();

    private readonly List<Vector3> _vertices = new List<Vector3>();
    private readonly List<int> _triangles = new List<int>();
    private readonly List<Vector2> _uvs = new List<Vector2>();
    private readonly List<Color> _colors = new List<Color>();

    private void Awake()
    {
        _meshFilter = GetComponent<MeshFilter>();
        _meshRenderer = GetComponent<MeshRenderer>();
        _dynamicMesh = new Mesh { name = "DynamicTrailMesh" };
        _meshFilter.mesh = _dynamicMesh;

        if (trailMaterial != null)
            _meshRenderer.material = trailMaterial;

        _meshRenderer.enabled = false;
    }

    /// <summary>
    /// Gọi từ Animation Event khi BẮT ĐẦU vung kiếm
    /// </summary>
    public void BeginSwing()
    {
        if (basePoint == null || tipPoint == null) return;

        // Nếu đòn trước chưa kết thúc mà bấm chiêu mới -> Nhả Trail đòn cũ ra
        if (_isSwinging)
        {
            TriggerArcSmear();
        }

        ResetState();

        _bladeLength = Vector3.Distance(basePoint.position, tipPoint.position) + tipExtension;
        _isSwinging = true;
        _swingTimer = 0f;
        _meshRenderer.enabled = true;

        RecordCurrentPositions();
    }

    /// <summary>
    /// Gọi từ Animation Event khi KẾT THÚC vung kiếm
    /// </summary>
    public void TriggerArcSmear()
    {
        if (!_isSwinging) return;

        _isSwinging = false;
        _meshRenderer.enabled = false;

        if (_rawPoints.Count >= 2)
        {
            GenerateSmoothedPoints();
            DetachAndFade();
        }

        ResetState();
    }

    private void ResetState()
    {
        _rawPoints.Clear();
        _smoothedPoints.Clear();
        if (_dynamicMesh != null)
        {
            _dynamicMesh.Clear();
        }
    }

    private void OnDisable()
    {
        _isSwinging = false;
        ResetState();
    }

    private void LateUpdate()
    {
        if (!_isSwinging) return;

        _swingTimer += Time.deltaTime;
        if (_swingTimer > maxSwingDuration)
        {
            TriggerArcSmear();
            return;
        }

        RecordCurrentPositions();
    }

    private void RecordCurrentPositions()
    {
        Vector3 curBase = basePoint.position;
        Vector3 curTipDir = (tipPoint.position - basePoint.position).normalized;
        if (curTipDir == Vector3.zero) curTipDir = transform.forward;
        Vector3 curTip = curBase + curTipDir * _bladeLength;

        if (_rawPoints.Count > 0)
        {
            TrailPoint lastPoint = _rawPoints[_rawPoints.Count - 1];

            // Kiểm tra xem gốc kiếm có bị dịch chuyển quá xa không (Teleport / Swap Animation)
            if (Vector3.Distance(lastPoint.BasePos, curBase) > maxAllowedBaseJump)
            {
                TriggerArcSmear();
                BeginSwing();
                return;
            }

            float distMoved = Vector3.Distance(lastPoint.TipPos, curTip);

            // Ghi nhận điểm mới nếu kiếm đã di chuyển
            if (distMoved >= minDistance)
            {
                // Tự động chèn thêm điểm phụ nếu vung kiếm siêu nhanh (Sub-frame)
                int subSteps = Mathf.Clamp(Mathf.FloorToInt(distMoved / minDistance), 1, 10);
                for (int i = 1; i <= subSteps; i++)
                {
                    float t = (float)i / subSteps;
                    Vector3 interpBase = Vector3.Lerp(lastPoint.BasePos, curBase, t);
                    Vector3 interpTip = Vector3.Lerp(lastPoint.TipPos, curTip, t);
                    AddRawPoint(interpBase, interpTip);
                }

                GenerateSmoothedPoints();
                UpdateDynamicMesh();
            }
        }
        else
        {
            AddRawPoint(curBase, curTip);
        }
    }

    private void AddRawPoint(Vector3 bPos, Vector3 tPos)
    {
        _rawPoints.Add(new TrailPoint { BasePos = bPos, TipPos = tPos });
    }

    private void GenerateSmoothedPoints()
    {
        _smoothedPoints.Clear();
        int count = _rawPoints.Count;

        if (count < 2) return;

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

            for (int step = 0; step < subdivisionsPerSegment; step++)
            {
                float t = (float)step / subdivisionsPerSegment;

                Vector3 smoothBase = CatmullRomHelper.GetPoint(p0.BasePos, p1.BasePos, p2.BasePos, p3.BasePos, t);
                Vector3 smoothTipRaw = CatmullRomHelper.GetPoint(p0.TipPos, p1.TipPos, p2.TipPos, p3.TipPos, t);

                Vector3 dir = (smoothTipRaw - smoothBase).normalized;
                if (dir == Vector3.zero) dir = transform.forward;

                Vector3 smoothTip = smoothBase + dir * _bladeLength;

                _smoothedPoints.Add(new TrailPoint { BasePos = smoothBase, TipPos = smoothTip });
            }
        }

        _smoothedPoints.Add(_rawPoints[count - 1]);
    }

    private void UpdateDynamicMesh()
    {
        if (_smoothedPoints.Count < 2)
        {
            _dynamicMesh.Clear();
            return;
        }

        _dynamicMesh.Clear();
        _vertices.Clear();
        _triangles.Clear();
        _uvs.Clear();
        _colors.Clear();

        int total = _smoothedPoints.Count;
        for (int i = 0; i < total; i++)
        {
            _vertices.Add(transform.InverseTransformPoint(_smoothedPoints[i].BasePos));
            _vertices.Add(transform.InverseTransformPoint(_smoothedPoints[i].TipPos));

            float progress = (float)i / (total - 1);
            _uvs.Add(new Vector2(progress, 0));
            _uvs.Add(new Vector2(progress, 1));

            float alphaProgress = Mathf.Sin(progress * Mathf.PI);
            _colors.Add(new Color(1f, 1f, 1f, alphaProgress * 0.1f));
            _colors.Add(new Color(1f, 1f, 1f, alphaProgress * 1.0f));
        }

        GenerateTriangles();

        _dynamicMesh.SetVertices(_vertices);
        _dynamicMesh.SetTriangles(_triangles, 0);
        _dynamicMesh.SetUVs(0, _uvs);
        _dynamicMesh.SetColors(_colors);

        _dynamicMesh.RecalculateNormals();
        _dynamicMesh.RecalculateBounds();
    }

    private void DetachAndFade()
    {
        GameObject smearObj = GetPooledFaderObject();

        Mesh detachedMesh = new Mesh { name = "StandaloneSmoothTrail" };
        List<Vector3> worldVerts = new List<Vector3>();
        List<Color> worldColors = new List<Color>();

        int total = _smoothedPoints.Count;
        for (int i = 0; i < total; i++)
        {
            worldVerts.Add(_smoothedPoints[i].BasePos);
            worldVerts.Add(_smoothedPoints[i].TipPos);

            float progress = (float)i / (total - 1);
            float alphaProgress = Mathf.Sin(progress * Mathf.PI);
            worldColors.Add(new Color(1f, 1f, 1f, alphaProgress * 0.1f));
            worldColors.Add(new Color(1f, 1f, 1f, alphaProgress * 1.0f));
        }

        detachedMesh.SetVertices(worldVerts);
        detachedMesh.SetTriangles(_triangles, 0);
        detachedMesh.SetUVs(0, _uvs);
        detachedMesh.SetColors(worldColors);
        detachedMesh.RecalculateNormals();
        detachedMesh.RecalculateBounds();

        MeshFilter mf = smearObj.GetComponent<MeshFilter>();
        MeshRenderer mr = smearObj.GetComponent<MeshRenderer>();
        mf.mesh = detachedMesh;
        mr.material = trailMaterial;

        StandaloneTrailFader fader = smearObj.GetComponent<StandaloneTrailFader>();
        fader.StartFade(fadeDuration, detachedMesh, mr, ReturnObjectToPool);
    }

    private GameObject GetPooledFaderObject()
    {
        GameObject obj;
        if (FaderPool.Count > 0)
        {
            obj = FaderPool.Dequeue();
            obj.SetActive(true);
        }
        else
        {
            obj = new GameObject("DetachedTrail_Pooled");
            obj.AddComponent<MeshFilter>();
            obj.AddComponent<MeshRenderer>();
            obj.AddComponent<StandaloneTrailFader>();
        }
        return obj;
    }

    private static void ReturnObjectToPool(GameObject obj)
    {
        obj.SetActive(false);
        FaderPool.Enqueue(obj);
    }

    private void GenerateTriangles()
    {
        for (int i = 0; i < _smoothedPoints.Count - 1; i++)
        {
            int idx = i * 2;

            _triangles.Add(idx);
            _triangles.Add(idx + 1);
            _triangles.Add(idx + 2);

            _triangles.Add(idx + 2);
            _triangles.Add(idx + 1);
            _triangles.Add(idx + 3);
        }
    }
}