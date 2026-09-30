#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Lerper))]
public class LerperEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        Lerper lerper = (Lerper)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Control Points Quick Tools", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Snap C1 <- Selected"))
        {
            SnapC1ToSelected(lerper);
        }

        if (GUILayout.Button("Snap C2 <- Selected"))
        {
            SnapC2ToSelected(lerper);
        }
        EditorGUILayout.EndHorizontal();
    }

    private void OnSceneGUI()
    {
        Lerper lerper = (Lerper)target;
        if (lerper == null || !lerper.DrawHandles) return;

        Undo.RecordObject(lerper, "Modify Lerper Bezier Curve");

        Vector3 worldPos1 = lerper.GetWorldPos1();
        Vector3 worldPos2 = lerper.GetWorldPos2();
        Quaternion worldRot1 = lerper.GetWorldRot1();
        Quaternion worldRot2 = lerper.GetWorldRot2();

        // Xác định hướng của Handle theo chế độ Toggle Pivot của Unity Editor (Local hoặc Global)
        Quaternion handleRot1 = (Tools.pivotRotation == PivotRotation.Local) ? worldRot1 : Quaternion.identity;
        Quaternion handleRot2 = (Tools.pivotRotation == PivotRotation.Local) ? worldRot2 : Quaternion.identity;

        // 1. TAY CẦM CONTROLLER 1 (Pos1 & Rot1)
        EditorGUI.BeginChangeCheck();
        Vector3 newWorldPos1 = Handles.PositionHandle(worldPos1, handleRot1);
        Quaternion newWorldRot1 = Handles.RotationHandle(worldRot1, worldPos1);
        if (EditorGUI.EndChangeCheck())
        {
            lerper.SetWorldPos1(newWorldPos1);
            lerper.SetWorldRot1(newWorldRot1);
            lerper.GenerateBezierMesh();
            EditorUtility.SetDirty(lerper);
        }

        // 2. TAY CẦM CONTROLLER 2 (Pos2 & Rot2)
        EditorGUI.BeginChangeCheck();
        Vector3 newWorldPos2 = Handles.PositionHandle(worldPos2, handleRot2);
        Quaternion newWorldRot2 = Handles.RotationHandle(worldRot2, worldPos2);
        if (EditorGUI.EndChangeCheck())
        {
            lerper.SetWorldPos2(newWorldPos2);
            lerper.SetWorldRot2(newWorldRot2);
            lerper.GenerateBezierMesh();
            EditorUtility.SetDirty(lerper);
        }

        // 3. TAY CẦM KÉO ĐỘ CONG R1 & R2 (Sphere Cap tự thích ứng kích thước)
        lerper.GetWorldControlPoints(out Vector3 p0, out Vector3 p1, out Vector3 p2, out Vector3 p3);

        Handles.color = Color.yellow;

        // Tính kích thước nút bấm linh hoạt theo khoảng cách Camera
        float sizeP1 = HandleUtility.GetHandleSize(p1) * 0.12f;
        EditorGUI.BeginChangeCheck();
        Vector3 newP1 = Handles.FreeMoveHandle(p1, sizeP1, Vector3.zero, Handles.SphereHandleCap);
        if (EditorGUI.EndChangeCheck())
        {
            lerper.R1 = Vector3.Distance(p0, newP1);
            lerper.GenerateBezierMesh();
            EditorUtility.SetDirty(lerper);
        }

        float sizeP2 = HandleUtility.GetHandleSize(p2) * 0.12f;
        EditorGUI.BeginChangeCheck();
        Vector3 newP2 = Handles.FreeMoveHandle(p2, sizeP2, Vector3.zero, Handles.SphereHandleCap);
        if (EditorGUI.EndChangeCheck())
        {
            lerper.R2 = Vector3.Distance(p3, newP2);
            lerper.GenerateBezierMesh();
            EditorUtility.SetDirty(lerper);
        }

        // Nhãn thông tin điểm Control Point
        Handles.Label(p0 + Vector3.up * (HandleUtility.GetHandleSize(p0) * 0.2f), $"Controller 1 ({lerper.Space})");
        Handles.Label(p3 + Vector3.up * (HandleUtility.GetHandleSize(p3) * 0.2f), $"Controller 2 ({lerper.Space})");

        // 4. BẢNG NÚT BẤM OVERLAY TRỰC TIẾP TRÊN SCENE VIEW
        DrawSceneGUIOverlay(lerper);
    }

    /// <summary>
    /// Vẽ bảng điều khiển nhỏ ở góc màn hình Scene View giúp thao tác cực nhanh
    /// </summary>
    private void DrawSceneGUIOverlay(Lerper lerper)
    {
        Handles.BeginGUI();

        // Tạo khung cửa sổ nhỏ góc trên bên trái Scene View
        GUILayout.BeginArea(new Rect(10, 10, 210, 160), "Bezier Control Tools", GUI.skin.window);

        Transform activeT = Selection.activeTransform;
        string selectedName = activeT != null ? activeT.name : "None";
        GUILayout.Label($"Selected: {selectedName}", EditorStyles.miniBoldLabel);

        EditorGUILayout.Space(2);

        if (GUILayout.Button("Snap C1 <- Selected"))
        {
            SnapC1ToSelected(lerper);
        }

        if (GUILayout.Button("Snap C2 <- Selected"))
        {
            SnapC2ToSelected(lerper);
        }

        if (GUILayout.Button("Move Selected -> C1"))
        {
            if (activeT != null)
            {
                Undo.RecordObject(activeT, "Move Selected to C1");
                activeT.position = lerper.GetWorldPos1();
                activeT.rotation = lerper.GetWorldRot1();
            }
        }

        if (GUILayout.Button("Move Selected -> C2"))
        {
            if (activeT != null)
            {
                Undo.RecordObject(activeT, "Move Selected to C2");
                activeT.position = lerper.GetWorldPos2();
                activeT.rotation = lerper.GetWorldRot2();
            }
        }

        GUILayout.EndArea();
        Handles.EndGUI();
    }

    private static void SnapC1ToSelected(Lerper lerper)
    {
        if (Selection.activeTransform != null)
        {
            Undo.RecordObject(lerper, "Snap C1 to Selected");
            lerper.SetWorldPos1(Selection.activeTransform.position);
            lerper.SetWorldRot1(Selection.activeTransform.rotation);
            lerper.GenerateBezierMesh();
            EditorUtility.SetDirty(lerper);
        }
        else
        {
            Debug.LogWarning("[Lerper] Hãy chọn một GameObject trong Scene trước!");
        }
    }

    private static void SnapC2ToSelected(Lerper lerper)
    {
        if (Selection.activeTransform != null)
        {
            Undo.RecordObject(lerper, "Snap C2 to Selected");
            lerper.SetWorldPos2(Selection.activeTransform.position);
            lerper.SetWorldRot2(Selection.activeTransform.rotation);
            lerper.GenerateBezierMesh();
            EditorUtility.SetDirty(lerper);
        }
        else
        {
            Debug.LogWarning("[Lerper] Hãy chọn một GameObject trong Scene trước!");
        }
    }
}
#endif