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
    [SerializeField] private float minDistance = 0.02f;
    [Range(1, 10)][SerializeField] private int subdivisionsPerSegment = 5;
    [SerializeField] private float fadeDuration = 0.2f;
    [SerializeField] private float tipExtension = 0.3f;
    [SerializeField] private float maxSwingDuration = 1.5f;
    [SerializeField] private float maxAllowedBaseJump = 15.0f;
    [SerializeField] private bool updateBladeLengthPerFrame = true;

    [Header("Speed Adaptation")]
    [Tooltip("Tốc độ (m/s) bắt đầu giảm smoothing và tăng mật độ điểm")]
    [SerializeField] private float speedThresholdLow = 2f;

    [Tooltip("Tốc độ (m/s) tại đó smoothing = 0 và mật độ điểm tối đa")]
    [SerializeField] private float speedThresholdHigh = 15f;

    [Tooltip("Số sub-step tối đa (cao để chịu được dash)")]
    [SerializeField] private int maxSubSteps = 256;

    [Header("Blend Animation Stability")]
    [Tooltip("Hệ số làm mượt khi tốc độ thấp (chống jitter blend)")]
    [Range(0.05f, 1f)][SerializeField] private float positionSmoothFactorLow = 0.4f;

    [Tooltip("Dùng camera forward làm reference winding")]
    [SerializeField] private bool useCameraReference = true;

    [Header("Material")]
    [SerializeField] private Material trailMaterial;

    private bool _isSwinging;
    private float _swingTimer;
    private float _bladeLength;
    private Mesh _dynamicMesh;
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;

    private bool _pendingBegin;
    private bool _pendingEnd;

    private Vector3 _swingDirection = Vector3.zero;
    private Vector3 _fixedFaceNormal = Vector3.zero;
    private Vector3 _cachedReferenceForward = Vector3.forward;

    // Smoothing state
    private Vector3 _smoothedBasePos;
    private Vector3 _smoothedTipPos;
    private Vector3 _prevRawBasePos;
    private bool _hasSmoothedPos;

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
        _dynamicMesh.MarkDynamic();
        _meshFilter.mesh = _dynamicMesh;

        if (trailMaterial != null)
            _meshRenderer.material = trailMaterial;

        _meshRenderer.enabled = false;
    }

    public void BeginSwing() { _pendingBegin = true; }
    public void TriggerArcSmear() { _pendingEnd = true; }

    // Ghi điểm ở cả Update và LateUpdate để không bỏ sót khi
    // nhân vật di chuyển bằng FixedUpdate hoặc Update
    private void Update()
    {
        if (_isSwinging) RecordCurrentPositions();
    }

    private void LateUpdate()
    {
        if (_pendingBegin)
        {
            _pendingBegin = false;
            ProcessBeginSwing();
        }

        if (_isSwinging)
        {
            _swingTimer += Time.deltaTime;
            if (_swingTimer > maxSwingDuration)
                ProcessTriggerArcSmear();
            else
                RecordCurrentPositions();
        }

        if (_pendingEnd)
        {
            _pendingEnd = false;
            if (_isSwinging)
                ProcessTriggerArcSmear();
        }
    }

    private void ProcessBeginSwing()
    {
        if (basePoint == null || tipPoint == null) return;
        if (_isSwinging) ProcessTriggerArcSmear();

        ResetState();

        _bladeLength = Vector3.Distance(basePoint.position, tipPoint.position) + tipExtension;
        _isSwinging = true;
        _swingTimer = 0f;
        _meshRenderer.enabled = true;
        _swingDirection = Vector3.zero;
        _fixedFaceNormal = Vector3.zero;
        _hasSmoothedPos = false;
        _prevRawBasePos = basePoint.position;

        _cachedReferenceForward = GetStableReferenceForward();

        RecordCurrentPositions();
    }

    private Vector3 GetStableReferenceForward()
    {
        if (useCameraReference && Camera.main != null)
            return Camera.main.transform.forward;
        if (transform.parent != null)
            return transform.parent.forward;
        return Vector3.forward;
    }

    private void ProcessTriggerArcSmear()
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
        _swingDirection = Vector3.zero;
        _fixedFaceNormal = Vector3.zero;
        _hasSmoothedPos = false;
        _prevRawBasePos = Vector3.zero;

        if (_dynamicMesh != null) _dynamicMesh.Clear();
    }

    private void OnDisable()
    {
        _isSwinging = false;
        _pendingBegin = false;
        _pendingEnd = false;
        ResetState();
    }

    private void RecordCurrentPositions()
    {
        Vector3 rawBase = basePoint.position;
        Vector3 rawTip = tipPoint.position;

        // === ADAPTIVE SMOOTHING ===
        // Tốc độ cao -> theo kịp ngay (smoothFactor = 1)
        // Tốc độ thấp -> smooth mạnh (chống jitter blend)
        float currentSpeed = Time.deltaTime > 0.0001f
            ? Vector3.Distance(rawBase, _prevRawBasePos) / Time.deltaTime
            : 0f;

        float speedT = Mathf.InverseLerp(speedThresholdLow, speedThresholdHigh, currentSpeed);
        float smoothFactor = Mathf.Lerp(positionSmoothFactorLow, 1f, speedT);

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

        if (updateBladeLengthPerFrame)
            _bladeLength = Vector3.Distance(_smoothedBasePos, _smoothedTipPos) + tipExtension;

        Vector3 curBase = _smoothedBasePos;
        Vector3 curTipDir = (_smoothedTipPos - _smoothedBasePos).normalized;
        if (curTipDir == Vector3.zero) curTipDir = _cachedReferenceForward;
        Vector3 curTip = curBase + curTipDir * _bladeLength;

        // Điểm đầu tiên
        if (_rawPoints.Count == 0)
        {
            AddRawPoint(curBase, curTip);
            return;
        }

        TrailPoint lastPoint = _rawPoints[_rawPoints.Count - 1];

        // Chỉ kết thúc trail khi thực sự teleport
        float baseJump = Vector3.Distance(lastPoint.BasePos, curBase);
        if (baseJump > maxAllowedBaseJump)
        {
            ProcessTriggerArcSmear();
            ProcessBeginSwing();
            return;
        }

        float distMoved = Vector3.Distance(lastPoint.TipPos, curTip);
        if (distMoved < minDistance * 0.1f) return; // dead zone nhỏ

        // Cập nhật hướng chém
        Vector3 stepDir = curTip - lastPoint.TipPos;
        if (stepDir.sqrMagnitude > 0.0001f)
        {
            Vector3 newDir = stepDir.normalized;
            _swingDirection = _swingDirection.sqrMagnitude < 0.0001f
                ? newDir
                : (_swingDirection + newDir).normalized;
        }

        // === ADAPTIVE MIN DISTANCE ===
        // Tốc độ cao -> minDistance nhỏ hơn -> nhiều điểm hơn -> mượt hơn
        float adaptiveMinDist = Mathf.Lerp(
            minDistance,
            minDistance * 0.25f,
            speedT
        );

        int subSteps = Mathf.CeilToInt(distMoved / adaptiveMinDist);
        subSteps = Mathf.Clamp(subSteps, 1, maxSubSteps);

        // === SLERP CHO HƯỚNG BLADE ===
        // Giữ độ cong tự nhiên khi nhân vật vừa chạy vừa xoay
        Vector3 lastDir = (lastPoint.TipPos - lastPoint.BasePos).normalized;
        Vector3 curDir = (curTip - curBase).normalized;
        if (lastDir == Vector3.zero) lastDir = curDir;
        if (curDir == Vector3.zero) curDir = lastDir;

        float lastBladeLen = Vector3.Distance(lastPoint.BasePos, lastPoint.TipPos);
        float curBladeLen = Vector3.Distance(curBase, curTip);

        for (int i = 1; i <= subSteps; i++)
        {
            float t = (float)i / subSteps;

            Vector3 interpBase = Vector3.Lerp(lastPoint.BasePos, curBase, t);
            Vector3 interpDir = Vector3.Slerp(lastDir, curDir, t).normalized;
            float interpLen = Mathf.Lerp(lastBladeLen, curBladeLen, t);
            Vector3 interpTip = interpBase + interpDir * interpLen;

            AddRawPoint(interpBase, interpTip);
        }

        UpdateFixedFaceNormal();
        GenerateSmoothedPoints();
        UpdateDynamicMesh();
    }

    private void AddRawPoint(Vector3 bPos, Vector3 tPos)
    {
        _rawPoints.Add(new TrailPoint { BasePos = bPos, TipPos = tPos });
    }

    private void UpdateFixedFaceNormal()
    {
        if (_fixedFaceNormal != Vector3.zero) return;
        if (_rawPoints.Count < 2) return;

        Vector3 swing = _rawPoints[_rawPoints.Count - 1].TipPos - _rawPoints[0].TipPos;
        if (swing.sqrMagnitude < 0.0001f) return;

        int mid = _rawPoints.Count / 2;
        Vector3 blade = _rawPoints[mid].TipPos - _rawPoints[mid].BasePos;
        if (blade.sqrMagnitude < 0.0001f) return;

        Vector3 normal = Vector3.Cross(swing.normalized, blade.normalized);
        if (normal.sqrMagnitude < 0.0001f) return;

        if (Vector3.Dot(normal, _cachedReferenceForward) < 0)
            normal = -normal;

        _fixedFaceNormal = normal.normalized;
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
                if (dir == Vector3.zero) dir = _cachedReferenceForward;

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

        // Cache matrix 1 lần — nhanh hơn InverseTransformPoint gọi 1000 lần
        Matrix4x4 worldToLocal = transform.worldToLocalMatrix;

        int total = _smoothedPoints.Count;
        for (int i = 0; i < total; i++)
        {
            _vertices.Add(worldToLocal.MultiplyPoint3x4(_smoothedPoints[i].BasePos));
            _vertices.Add(worldToLocal.MultiplyPoint3x4(_smoothedPoints[i].TipPos));

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

    private void GenerateTriangles()
    {
        bool flip = ComputeWindingFlip();

        for (int i = 0; i < _smoothedPoints.Count - 1; i++)
        {
            int idx = i * 2;

            if (!flip)
            {
                _triangles.Add(idx);
                _triangles.Add(idx + 1);
                _triangles.Add(idx + 2);
                _triangles.Add(idx + 2);
                _triangles.Add(idx + 1);
                _triangles.Add(idx + 3);
            }
            else
            {
                _triangles.Add(idx);
                _triangles.Add(idx + 2);
                _triangles.Add(idx + 1);
                _triangles.Add(idx + 2);
                _triangles.Add(idx + 3);
                _triangles.Add(idx + 1);
            }
        }
    }

    private bool ComputeWindingFlip()
    {
        if (_smoothedPoints.Count < 2) return false;

        Vector3 swing = _smoothedPoints[_smoothedPoints.Count - 1].TipPos
                      - _smoothedPoints[0].TipPos;

        int mid = _smoothedPoints.Count / 2;
        Vector3 blade = _smoothedPoints[mid].TipPos - _smoothedPoints[mid].BasePos;

        if (swing.sqrMagnitude < 0.0001f || blade.sqrMagnitude < 0.0001f)
            return false;

        Vector3 currentNormal = Vector3.Cross(swing.normalized, blade.normalized);
        if (currentNormal.sqrMagnitude < 0.0001f) return false;

        if (_fixedFaceNormal != Vector3.zero)
            return Vector3.Dot(currentNormal, _fixedFaceNormal) < 0f;

        return Vector3.Dot(currentNormal, _cachedReferenceForward) < 0f;
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
}