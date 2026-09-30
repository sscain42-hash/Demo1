#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SlashDataSO))]
public class SlashDataSOEditor : Editor
{
    private SlashDataSO _targetSO;

    private bool _showPositionHandles = true;
    private bool _showRotationHandles = true;
    private bool _showFanHeightHandles = true;

    private Vector3 _previewOrigin = Vector3.zero;
    private Quaternion _previewRotation = Quaternion.identity;

    private Mesh _previewMesh;
    private float _previewProgress = 1.0f;
    private bool _isAnimating = false;
    private float _animSpeed = 1.5f;
    private double _lastTime;

    private void OnEnable()
    {
        _targetSO = (SlashDataSO)target;
        SceneView.duringSceneGui += OnSceneGUIHandler;
        EditorApplication.update += UpdateAnimation;

        if (_previewMesh == null)
        {
            _previewMesh = new Mesh { name = "SlashEditorPreviewMesh" };
        }
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUIHandler;
        EditorApplication.update -= UpdateAnimation;

        if (_previewMesh != null)
        {
            DestroyImmediate(_previewMesh);
        }
    }

    private void UpdateAnimation()
    {
        if (!_isAnimating) return;

        double currentTime = EditorApplication.timeSinceStartup;
        float deltaTime = (float)(currentTime - _lastTime);
        _lastTime = currentTime;

        _previewProgress += deltaTime * _animSpeed;
        if (_previewProgress > 1f)
        {
            _previewProgress = 0f;
        }

        SceneView.RepaintAll();
        Repaint();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUI.BeginChangeCheck();

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("⚙️ General Mesh Settings", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("segmentsPerSection"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("doubleSided"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("slashMaterial"));

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("🎬 Visual 3D Preview & Playback", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        _previewProgress = EditorGUILayout.Slider("Preview Progress", _previewProgress, 0f, 1f);
        _animSpeed = EditorGUILayout.Slider("Anim Speed", _animSpeed, 0.2f, 5f);

        EditorGUILayout.BeginHorizontal();
        string playButtonText = _isAnimating ? "⏸ Pause Animation" : "▶ Play Animation";
        if (GUILayout.Button(playButtonText, GUILayout.Height(28)))
        {
            _isAnimating = !_isAnimating;
            _lastTime = EditorApplication.timeSinceStartup;
        }

        if (GUILayout.Button("⏹ Reset (1.0)", GUILayout.Height(28), GUILayout.Width(90)))
        {
            _isAnimating = false;
            _previewProgress = 1f;
            SceneView.RepaintAll();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("🛠️ Quick Actions / Utilities", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("🔄 Flip Path (Đảo Chiều Chém)", GUILayout.Height(26))) FlipPathDirection();
        if (GUILayout.Button("↕️ Flip Up/Down (Lật Ngược)", GUILayout.Height(26))) FlipUpsideDown();

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("🎯 Scene View Handle Controls", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        _showPositionHandles = EditorGUILayout.ToggleLeft("Pos Handle", _showPositionHandles, GUILayout.Width(90));
        _showRotationHandles = EditorGUILayout.ToggleLeft("Rot Handle", _showRotationHandles, GUILayout.Width(90));
        _showFanHeightHandles = EditorGUILayout.ToggleLeft("Height Handle", _showFanHeightHandles, GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();

        _previewOrigin = EditorGUILayout.Vector3Field("Preview Center Position", _previewOrigin);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("📌 Control Points List", EditorStyles.boldLabel);

        if (_targetSO.controlPoints != null)
        {
            for (int i = 0; i < _targetSO.controlPoints.Count; i++)
            {
                ControlPointData point = _targetSO.controlPoints[i];

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Point {i}", EditorStyles.boldLabel, GUILayout.Width(55));

                // NÚT LẤY TỎA ĐỘ TỪ OBJECT ĐANG CHỌN (SELECTION)
                if (GUILayout.Button("<- Selected", EditorStyles.miniButton, GUILayout.Width(75)))
                {
                    SnapPointFromSelected(i);
                }

                // NÚT DI CHUYỂN OBJECT ĐANG CHỌN TỚI ĐIỂM NÀY
                if (GUILayout.Button("-> Selected", EditorStyles.miniButton, GUILayout.Width(75)))
                {
                    MoveSelectedToPoint(i);
                }

                if (GUILayout.Button("Reset Rot", EditorStyles.miniButton, GUILayout.Width(65)))
                {
                    Undo.RecordObject(_targetSO, $"Reset Rotation Point {i}");
                    point.localEulerAngles = Vector3.zero;
                    _targetSO.controlPoints[i] = point;
                }

                if (GUILayout.Button("Xóa", EditorStyles.miniButton, GUILayout.Width(40)) && _targetSO.controlPoints.Count > 2)
                {
                    Undo.RecordObject(_targetSO, $"Remove Point {i}");
                    _targetSO.controlPoints.RemoveAt(i);
                    EditorUtility.SetDirty(_targetSO);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }
                EditorGUILayout.EndHorizontal();

                Vector3 newPos = EditorGUILayout.Vector3Field("Position", point.localPosition);

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Rotation (Euler Angles)", EditorStyles.miniBoldLabel);

                Vector3 euler = point.localEulerAngles;
                euler.x = FormatAngle(euler.x);
                euler.y = FormatAngle(euler.y);
                euler.z = FormatAngle(euler.z);

                EditorGUI.indentLevel++;
                float rotX = EditorGUILayout.Slider("Pitch (X)", euler.x, -180f, 180f);
                float rotY = EditorGUILayout.Slider("Yaw (Y)", euler.y, -180f, 180f);
                float rotZ = EditorGUILayout.Slider("Roll (Z)", euler.z, -180f, 180f);
                EditorGUI.indentLevel--;
                Vector3 newEuler = new Vector3(rotX, rotY, rotZ);

                EditorGUILayout.Space(2);
                float newHeight = EditorGUILayout.Slider("Fan Height", point.fanHeight, 0.1f, 10f);

                if (newPos != point.localPosition || newEuler != point.localEulerAngles || !Mathf.Approximately(newHeight, point.fanHeight))
                {
                    Undo.RecordObject(_targetSO, $"Modify Control Point {i}");
                    point.localPosition = newPos;
                    point.localEulerAngles = newEuler;
                    point.fanHeight = newHeight;
                    _targetSO.controlPoints[i] = point;
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }
        }

        EditorGUILayout.Space(5);
        if (GUILayout.Button("➕ Add Control Point", GUILayout.Height(28)))
        {
            Undo.RecordObject(_targetSO, "Add Control Point");
            Vector3 lastPos = _targetSO.controlPoints.Count > 0
                ? _targetSO.controlPoints[_targetSO.controlPoints.Count - 1].localPosition + Vector3.right
                : Vector3.zero;
            _targetSO.controlPoints.Add(new ControlPointData(lastPos, Vector3.zero, 1.5f));
            EditorUtility.SetDirty(_targetSO);
        }

        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(_targetSO);
            SceneView.RepaintAll();
        }
    }

    private void OnSceneGUIHandler(SceneView sceneView)
    {
        if (_targetSO == null || _targetSO.controlPoints == null || _targetSO.controlPoints.Count < 2) return;

        // 1. Gọi trực tiếp Utility chung để đồng bộ 100% với Runtime
        SlashMeshUtility.GenerateMesh(_targetSO, _previewMesh, _previewProgress);
        RenderPreviewMesh();

        // 2. Vẽ đường khung Gizmos hỗ trợ căn chỉnh
        DrawCurvePreview();

        // 3. Vẽ Bảng nút thao tác nhanh Overlay trên Scene View
        DrawSceneGUIOverlay();

        // 4. Vẽ Handles cho từng Control Point
        for (int i = 0; i < _targetSO.controlPoints.Count; i++)
        {
            ControlPointData point = _targetSO.controlPoints[i];

            Vector3 worldSpinePos = _previewOrigin + (_previewRotation * point.localPosition);
            Quaternion worldRot = _previewRotation * point.LocalRotation;

            Handles.Label(worldSpinePos + Vector3.up * 0.2f, $"Point {i}", EditorStyles.boldLabel);

            Handles.color = Color.blue;
            Handles.DrawLine(worldSpinePos, worldSpinePos + (worldRot * Vector3.forward * 0.4f));
            Handles.color = Color.green;
            Handles.DrawLine(worldSpinePos, worldSpinePos + (worldRot * Vector3.up * 0.4f));

            EditorGUI.BeginChangeCheck();

            Vector3 newWorldPos = worldSpinePos;
            Quaternion newWorldRot = worldRot;

            if (_showPositionHandles) newWorldPos = Handles.PositionHandle(worldSpinePos, worldRot);
            if (_showRotationHandles) newWorldRot = Handles.RotationHandle(worldRot, worldSpinePos);

            float newFanHeight = point.fanHeight;
            if (_showFanHeightHandles)
            {
                Vector3 topWorldPos = worldSpinePos + (worldRot * Vector3.up * point.fanHeight);
                Handles.color = Color.green;
                Handles.DrawLine(worldSpinePos, topWorldPos);

                newFanHeight = Handles.ScaleValueHandle(
                    point.fanHeight,
                    topWorldPos,
                    worldRot * Quaternion.LookRotation(Vector3.up),
                    HandleUtility.GetHandleSize(topWorldPos) * 0.8f,
                    Handles.ConeHandleCap,
                    0.1f
                );
            }

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_targetSO, $"Modify Control Point {i}");

                Vector3 newLocalPos = Quaternion.Inverse(_previewRotation) * (newWorldPos - _previewOrigin);
                Quaternion newLocalRot = Quaternion.Inverse(_previewRotation) * newWorldRot;

                point.localPosition = newLocalPos;
                point.localEulerAngles = newLocalRot.eulerAngles;
                point.fanHeight = Mathf.Max(0.1f, newFanHeight);

                _targetSO.controlPoints[i] = point;
                EditorUtility.SetDirty(_targetSO);
            }
        }
    }

    /// <summary>
    /// Vẽ Overlay GUI trực tiếp trên Scene View
    /// </summary>
    private void DrawSceneGUIOverlay()
    {
        Handles.BeginGUI();
        GUILayout.BeginArea(new Rect(10, 10, 220, 150), "Slash Control Tools", GUI.skin.window);

        Transform activeT = Selection.activeTransform;
        string selectedName = activeT != null ? activeT.name : "None";
        GUILayout.Label($"Selected: {selectedName}", EditorStyles.miniBoldLabel);

        EditorGUILayout.Space(2);

        if (_targetSO.controlPoints != null && _targetSO.controlPoints.Count > 0)
        {
            if (GUILayout.Button("Snap Point 0 <- Selected"))
            {
                SnapPointFromSelected(0);
            }

            int lastIdx = _targetSO.controlPoints.Count - 1;
            if (GUILayout.Button($"Snap Point {lastIdx} <- Selected"))
            {
                SnapPointFromSelected(lastIdx);
            }
        }

        EditorGUILayout.Space(2);
        _showPositionHandles = GUILayout.Toggle(_showPositionHandles, " Show Pos Handles");
        _showRotationHandles = GUILayout.Toggle(_showRotationHandles, " Show Rot Handles");

        GUILayout.EndArea();
        Handles.EndGUI();
    }

    /// <summary>
    /// Lấy vị trí & hướng xoay từ GameObject đang chọn gán vào Point [index]
    /// </summary>
    private void SnapPointFromSelected(int index)
    {
        if (Selection.activeTransform == null)
        {
            Debug.LogWarning("[SlashDataSO] Hãy chọn một GameObject trong Scene trước!");
            return;
        }

        Undo.RecordObject(_targetSO, $"Snap Point {index} from Selected");
        Transform activeT = Selection.activeTransform;

        Vector3 worldPos = activeT.position;
        Quaternion worldRot = activeT.rotation;

        // Quy đổi từ World Space sang Local Space của Slash Preview
        Vector3 localPos = Quaternion.Inverse(_previewRotation) * (worldPos - _previewOrigin);
        Quaternion localRot = Quaternion.Inverse(_previewRotation) * worldRot;

        ControlPointData point = _targetSO.controlPoints[index];
        point.localPosition = localPos;
        point.localEulerAngles = localRot.eulerAngles;

        _targetSO.controlPoints[index] = point;
        EditorUtility.SetDirty(_targetSO);
        SceneView.RepaintAll();
    }

    /// <summary>
    /// Di chuyển GameObject đang chọn tới vị trí World của Point [index]
    /// </summary>
    private void MoveSelectedToPoint(int index)
    {
        if (Selection.activeTransform == null)
        {
            Debug.LogWarning("[SlashDataSO] Hãy chọn một GameObject trong Scene trước!");
            return;
        }

        Transform activeT = Selection.activeTransform;
        Undo.RecordObject(activeT, $"Move Selected to Point {index}");

        ControlPointData point = _targetSO.controlPoints[index];
        Vector3 worldSpinePos = _previewOrigin + (_previewRotation * point.localPosition);
        Quaternion worldRot = _previewRotation * point.LocalRotation;

        activeT.position = worldSpinePos;
        activeT.rotation = worldRot;
    }

    private void RenderPreviewMesh()
    {
        if (_previewMesh == null || _targetSO.slashMaterial == null) return;

        Matrix4x4 matrix = Matrix4x4.TRS(_previewOrigin, _previewRotation, Vector3.one);
        Graphics.DrawMesh(_previewMesh, matrix, _targetSO.slashMaterial, 0);
    }

    private void DrawCurvePreview()
    {
        int segments = _targetSO.controlPoints.Count - 1;
        int stepsPerSegment = Mathf.Max(4, _targetSO.segmentsPerSection);

        Vector3 prevBottom = Vector3.zero;
        Vector3 prevTop = Vector3.zero;

        for (int i = 0; i <= segments * stepsPerSegment; i++)
        {
            float t = (float)i / (segments * stepsPerSegment);

            Vector3 localBottom = SlashMeshUtility.GetSpinePointLocal(_targetSO, t);
            Vector3 localTop = SlashMeshUtility.GetTopPointLocal(_targetSO, t);

            Vector3 worldBottom = _previewOrigin + (_previewRotation * localBottom);
            Vector3 worldTop = _previewOrigin + (_previewRotation * localTop);

            if (i > 0)
            {
                Handles.color = Color.cyan;
                Handles.DrawLine(prevBottom, worldBottom, 2.5f);

                Handles.color = Color.magenta;
                Handles.DrawLine(prevTop, worldTop, 2.5f);

                Handles.color = new Color(1f, 1f, 1f, 0.25f);
                Handles.DrawLine(worldBottom, worldTop);
            }

            prevBottom = worldBottom;
            prevTop = worldTop;
        }
    }

    private void FlipPathDirection()
    {
        if (_targetSO.controlPoints == null || _targetSO.controlPoints.Count < 2) return;
        Undo.RecordObject(_targetSO, "Flip Slash Path Direction");
        _targetSO.controlPoints.Reverse();
        for (int i = 0; i < _targetSO.controlPoints.Count; i++)
        {
            ControlPointData point = _targetSO.controlPoints[i];
            Vector3 euler = point.localEulerAngles;
            euler.y = FormatAngle(euler.y + 180f);
            point.localEulerAngles = euler;
            _targetSO.controlPoints[i] = point;
        }
        EditorUtility.SetDirty(_targetSO);
        SceneView.RepaintAll();
    }

    private void FlipUpsideDown()
    {
        if (_targetSO.controlPoints == null || _targetSO.controlPoints.Count == 0) return;
        Undo.RecordObject(_targetSO, "Flip Slash Upside Down");
        for (int i = 0; i < _targetSO.controlPoints.Count; i++)
        {
            ControlPointData point = _targetSO.controlPoints[i];
            Vector3 euler = point.localEulerAngles;
            euler.z = FormatAngle(euler.z + 180f);
            point.localEulerAngles = euler;
            _targetSO.controlPoints[i] = point;
        }
        EditorUtility.SetDirty(_targetSO);
        SceneView.RepaintAll();
    }

    private float FormatAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }
}
#endif