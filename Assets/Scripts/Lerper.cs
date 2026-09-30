using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Lerper : MonoBehaviour
{
    public enum CurveSpace { Local, World }

    [Header("Space Settings")]
    public CurveSpace Space = CurveSpace.Local;

    [Header("Progression & Parameters")]
    [Range(0f, 1f)] public float Progression = 1f;
    [Tooltip("Độ dài lưỡi kiếm (Độ tỏa rộng của quạt)")]
    public float ScaleConstant = 1.2f;
    [Tooltip("Khoảng cách tiếp tuyến kéo dài từ điểm đầu")]
    public float R1 = 1f;
    [Tooltip("Khoảng cách tiếp tuyến kéo dài từ điểm cuối")]
    public float R2 = 1.08f;
    [Range(2, 100)] public int NPoints = 20;

    [Header("Controllers (2 Điểm định hình Bezier)")]
    public Vector3 Pos1 = new Vector3(-0.5f, 0, 0);
    public Vector3 Rot1 = Vector3.zero;
    public Vector3 Pos2 = new Vector3(0.5f, 0, 0);
    public Vector3 Rot2 = Vector3.zero;

    [Header("Visualization Toggles")]
    public bool DrawAxes = true;
    public bool DrawMesh = true;
    public bool DrawWireframe = true;
    public bool DrawHandles = true;

    [Header("Material")]
    public Material SlashMaterial;

    private Mesh _mesh;
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;

    public Vector3 GetWorldPos1() => Space == CurveSpace.Local ? transform.TransformPoint(Pos1) : Pos1;
    public Vector3 GetWorldPos2() => Space == CurveSpace.Local ? transform.TransformPoint(Pos2) : Pos2;

    public Quaternion GetWorldRot1() => Space == CurveSpace.Local ? transform.rotation * Quaternion.Euler(Rot1) : Quaternion.Euler(Rot1);
    public Quaternion GetWorldRot2() => Space == CurveSpace.Local ? transform.rotation * Quaternion.Euler(Rot2) : Quaternion.Euler(Rot2);

    public void SetWorldPos1(Vector3 worldPos) => Pos1 = Space == CurveSpace.Local ? transform.InverseTransformPoint(worldPos) : worldPos;
    public void SetWorldPos2(Vector3 worldPos) => Pos2 = Space == CurveSpace.Local ? transform.InverseTransformPoint(worldPos) : worldPos;

    public void SetWorldRot1(Quaternion worldRot)
    {
        Quaternion localRot = Space == CurveSpace.Local ? Quaternion.Inverse(transform.rotation) * worldRot : worldRot;
        Rot1 = localRot.eulerAngles;
    }

    public void SetWorldRot2(Quaternion worldRot)
    {
        Quaternion localRot = Space == CurveSpace.Local ? Quaternion.Inverse(transform.rotation) * worldRot : worldRot;
        Rot2 = localRot.eulerAngles;
    }

    private void OnEnable() => InitComponents();

    private void InitComponents()
    {
        if (_meshFilter == null) _meshFilter = GetComponent<MeshFilter>();
        if (_meshRenderer == null) _meshRenderer = GetComponent<MeshRenderer>();

        if (_mesh == null)
        {
            _mesh = new Mesh { name = "BezierSlashMesh" };
            if (_meshFilter != null) _meshFilter.sharedMesh = _mesh;
        }

        if (SlashMaterial != null && _meshRenderer != null)
        {
            _meshRenderer.sharedMaterial = SlashMaterial;
        }
    }

    private void Update()
    {
        if (DrawMesh)
        {
            GenerateBezierMesh();
        }
        else if (_mesh != null)
        {
            _mesh.Clear();
        }
    }

    public void GetWorldControlPoints(out Vector3 p0, out Vector3 p1, out Vector3 p2, out Vector3 p3)
    {
        p0 = GetWorldPos1();
        p3 = GetWorldPos2();

        Vector3 dir1 = GetWorldRot1() * Vector3.forward;
        Vector3 dir2 = GetWorldRot2() * Vector3.forward;

        p1 = p0 + dir1 * R1;
        p2 = p3 - dir2 * R2;
    }

    /// <summary>
    /// Khóa hướng quạt 100% theo Trục Y và Z của Controller 1.
    /// Không bao giờ bị lật ngược dù vòm quạt rộng đến đâu.
    /// </summary>
    public Vector3 GetFanDirectionAtTime(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        Quaternion rot1 = GetWorldRot1();
        Vector3 initialForward = rot1 * Vector3.forward; // Trục Z gốc
        Vector3 initialUp = rot1 * Vector3.up;          // Trục Y gốc (Hướng quạt)

        Vector3 currentTangent = GetBezierTangent(p0, p1, p2, p3, t);

        if (currentTangent.sqrMagnitude < 0.0001f)
            return initialUp;

        // Xoay hướng quạt ban đầu (Trục Y) bám theo tiếp tuyến đường cong (Trục Z)
        Quaternion rotationFromStart = Quaternion.FromToRotation(initialForward, currentTangent);
        return rotationFromStart * initialUp;
    }

    public void GenerateBezierMesh()
    {
        InitComponents();
        if (_mesh == null) return;

        GetWorldControlPoints(out Vector3 p0, out Vector3 p1, out Vector3 p2, out Vector3 p3);

        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        int activeSegments = Mathf.Max(1, Mathf.FloorToInt(NPoints * Progression));

        for (int i = 0; i <= activeSegments; i++)
        {
            float t = (float)i / NPoints;
            Vector3 centerWorldPos = GetBezierPoint(p0, p1, p2, p3, t);

            // TÍNH HƯỚNG TỎA QUẠT AN TOÀN THEO CONTROLLER 1
            Vector3 fanDirection = GetFanDirectionAtTime(p0, p1, p2, p3, t);

            float taperFactor = Mathf.Sin((float)i / activeSegments * Mathf.PI);
            float currentLength = ScaleConstant * taperFactor;

            Vector3 innerWorld = centerWorldPos;
            Vector3 outerWorld = centerWorldPos + fanDirection * currentLength;

            vertices.Add(transform.InverseTransformPoint(innerWorld));
            vertices.Add(transform.InverseTransformPoint(outerWorld));

            uvs.Add(new Vector2(t, 0f));
            uvs.Add(new Vector2(t, 1f));
        }

        for (int i = 0; i < activeSegments; i++)
        {
            int idx = i * 2;
            triangles.Add(idx);
            triangles.Add(idx + 1);
            triangles.Add(idx + 2);

            triangles.Add(idx + 2);
            triangles.Add(idx + 1);
            triangles.Add(idx + 3);
        }

        _mesh.Clear();
        _mesh.SetVertices(vertices);
        _mesh.SetTriangles(triangles, 0);
        _mesh.SetUVs(0, uvs);
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();
    }

    public static Vector3 GetBezierPoint(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
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

    public static Vector3 GetBezierTangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float u = 1f - t;
        Vector3 tangent = 3f * u * u * (p1 - p0)
                        + 6f * u * t * (p2 - p1)
                        + 3f * t * t * (p3 - p2);
        return tangent.normalized;
    }

    private void OnDrawGizmos()
    {
        GetWorldControlPoints(out Vector3 p0, out Vector3 p1, out Vector3 p2, out Vector3 p3);

        int activeSegments = Mathf.Max(1, Mathf.FloorToInt(NPoints * Progression));

        if (DrawWireframe)
        {
            Gizmos.color = Color.yellow;

            Vector3 prevInner = Vector3.zero;
            Vector3 prevOuter = Vector3.zero;

            for (int i = 0; i <= activeSegments; i++)
            {
                float t = (float)i / NPoints;
                Vector3 centerWorldPos = GetBezierPoint(p0, p1, p2, p3, t);

                Vector3 fanDirection = GetFanDirectionAtTime(p0, p1, p2, p3, t);

                float taperFactor = Mathf.Sin((float)i / activeSegments * Mathf.PI);
                float currentLength = ScaleConstant * taperFactor;

                Vector3 innerWorld = centerWorldPos;
                Vector3 outerWorld = centerWorldPos + fanDirection * currentLength;

                Gizmos.DrawLine(innerWorld, outerWorld);

                if (i > 0)
                {
                    Gizmos.DrawLine(prevInner, innerWorld);
                    Gizmos.DrawLine(prevOuter, outerWorld);
                }

                prevInner = innerWorld;
                prevOuter = outerWorld;
            }
        }

        if (DrawHandles)
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.5f);
            Gizmos.DrawLine(p0, p1);
            Gizmos.DrawLine(p3, p2);
            Gizmos.DrawWireSphere(p1, 0.03f);
            Gizmos.DrawWireSphere(p2, 0.03f);
        }

        if (DrawAxes)
        {
            DrawControllerAxis(p0, GetWorldRot1());
            DrawControllerAxis(p3, GetWorldRot2());
        }
    }

    private void DrawControllerAxis(Vector3 pos, Quaternion rot)
    {
#if UNITY_EDITOR
        float size = UnityEditor.HandleUtility.GetHandleSize(pos) * 0.25f;
#else
        float size = 0.3f;
#endif
        Gizmos.color = Color.red;
        Gizmos.DrawRay(pos, rot * Vector3.right * size);
        Gizmos.color = Color.green;
        Gizmos.DrawRay(pos, rot * Vector3.up * size);
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(pos, rot * Vector3.forward * size);
    }
}