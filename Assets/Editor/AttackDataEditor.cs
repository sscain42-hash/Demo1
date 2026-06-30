#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AttackData))]
public class AttackDataEditor : Editor
{
    private readonly string[] _actionPresets = { "ComboInputBuffer", "DashCancel", "JumpCancel" };
    private bool _showWindows = true;

    public override void OnInspectorGUI()
    {
        // 🌟 BẮT BUỘC: Cập nhật trạng thái đối tượng được serialization liên tục mỗi frame vẽ
        serializedObject.Update();

        // 1. Vẽ các thuộc tính gốc
        DrawPropertiesExcluding(serializedObject, "windows");

        EditorGUILayout.Space(10);
        SerializedProperty windowsProp = serializedObject.FindProperty("windows");

        if (windowsProp == null)
        {
            EditorGUILayout.HelpBox("Không tìm thấy thuộc tính 'windows' trong AttackData. Hãy kiểm tra lại tên biến gốc!", MessageType.Error);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        // 2. Foldout quản lý danh sách Windows
        _showWindows = EditorGUILayout.Foldout(_showWindows, $"⏱ Action Windows ({windowsProp.arraySize})", true, EditorStyles.foldoutHeader);

        if (_showWindows)
        {
            EditorGUI.indentLevel++;

            for (int i = 0; i < windowsProp.arraySize; i++)
            {
                SerializedProperty windowRef = windowsProp.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // --- HÀNG 1: Tên Action & Duplicate & Xóa ---
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("▲", GUILayout.Width(20)) && i > 0)
                {
                    windowsProp.MoveArrayElement(i, i - 1);
                    serializedObject.ApplyModifiedProperties();
                    GUIUtility.ExitGUI();
                }

                if (GUILayout.Button("▼", GUILayout.Width(20)) && i < windowsProp.arraySize - 1)
                {
                    windowsProp.MoveArrayElement(i, i + 1);
                    serializedObject.ApplyModifiedProperties();
                    GUIUtility.ExitGUI();
                }

                SerializedProperty nameProp = windowRef.FindPropertyRelative("actionName");

                int selectedIndex = -1;
                for (int j = 0; j < _actionPresets.Length; j++)
                {
                    if (nameProp.stringValue == _actionPresets[j]) { selectedIndex = j; break; }
                }

                int displayIndex = (selectedIndex == -1) ? _actionPresets.Length : selectedIndex;
                EditorGUI.BeginChangeCheck();
                int newIndex = EditorGUILayout.Popup(displayIndex, AppendCustomOption(_actionPresets), GUILayout.Width(100));

                if (EditorGUI.EndChangeCheck())
                {
                    nameProp.stringValue = (newIndex < _actionPresets.Length) ? _actionPresets[newIndex] : "";
                }

                if (selectedIndex == -1)
                {
                    nameProp.stringValue = EditorGUILayout.TextField(nameProp.stringValue);
                }

                // NÚT DUPLICATE
                GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);
                GUIContent dupIcon = EditorGUIUtility.IconContent("TreeEditor.Duplicate");
                dupIcon.tooltip = "Duplicate this Action Window";
                if (GUILayout.Button(dupIcon, GUILayout.Width(30), GUILayout.Height(22)))
                {
                    windowsProp.InsertArrayElementAtIndex(i);
                    serializedObject.ApplyModifiedProperties();
                    GUIUtility.ExitGUI();
                }

                // NÚT XÓA
                GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
                if (GUILayout.Button("✕", GUILayout.Width(25)))
                {
                    windowsProp.DeleteArrayElementAtIndex(i);
                    serializedObject.ApplyModifiedProperties();
                    GUIUtility.ExitGUI();
                }

                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();

                // --- HÀNG 2: Time Range Slider ---
                SerializedProperty startProp = windowRef.FindPropertyRelative("startTime");
                SerializedProperty endProp = windowRef.FindPropertyRelative("endTime");
                float start = startProp.floatValue;
                float end = endProp.floatValue;

                EditorGUILayout.MinMaxSlider(new GUIContent("Time Range"), ref start, ref end, 0f, 1f);
                startProp.floatValue = start;
                endProp.floatValue = end;

                // --- 🎯 HÀNG 3: KHU VỰC CẤU HÌNH LUNGE MỚI THÔNG MINH ---
                SerializedProperty enableLungeProp = windowRef.FindPropertyRelative("enableLunge");

                // Sử dụng EditorGUI.BeginChangeCheck để bắt sự kiện click thay đổi nút tích ngay lập tức
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(enableLungeProp, new GUIContent("💥 Enable Smart Lunge", "Bật chế độ lao đến khóa mục tiêu thông minh"));
                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                }

                if (enableLungeProp.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                    // Vẽ an toàn kèm kiểm tra thuộc tính để tránh NullReference gãy Layout hệ thống
                    var pSpeed = windowRef.FindPropertyRelative("lungeSpeed");
                    var pMaxDist = windowRef.FindPropertyRelative("maxLungeDistance");
                    var pKeepDist = windowRef.FindPropertyRelative("keepDistanceOffset");
                    var pLayer = windowRef.FindPropertyRelative("enemyLayer");

                    if (pSpeed != null) EditorGUILayout.PropertyField(pSpeed, new GUIContent("Lunge Speed (m/s)"));
                    if (pMaxDist != null) EditorGUILayout.PropertyField(pMaxDist, new GUIContent("Max Distance (m)"));
                    if (pKeepDist != null) EditorGUILayout.PropertyField(pKeepDist, new GUIContent("Keep Distance (m)"));
                    if (pLayer != null) EditorGUILayout.PropertyField(pLayer, new GUIContent("Enemy Layer"));

                    EditorGUILayout.EndVertical();
                    EditorGUI.indentLevel--;
                }
                else
                {
                    var pTargetDist = windowRef.FindPropertyRelative("targetDistance");
                    if (pTargetDist != null) EditorGUILayout.PropertyField(pTargetDist, new GUIContent("Target Distance (Local)"));
                }

                // --- HÀNG 4: VFX Config ---
                SerializedProperty enableVFXProp = windowRef.FindPropertyRelative("enableVFX");
                EditorGUILayout.PropertyField(enableVFXProp);

                if (enableVFXProp.boolValue)
                {
                    EditorGUI.indentLevel++;
                    SerializedProperty vfxData = windowRef.FindPropertyRelative("vfxTransform");
                    if (GUILayout.Button("Copy from Selection", GUILayout.Height(20)))
                    {
                        if (Selection.activeGameObject != null)
                        {
                            Transform t = Selection.activeGameObject.transform;
                            vfxData.FindPropertyRelative("positionOffset").vector3Value = t.localPosition;
                            vfxData.FindPropertyRelative("rotationOffset").vector3Value = t.localEulerAngles;
                            vfxData.FindPropertyRelative("scale").vector3Value = t.localScale;
                        }
                    }
                    if (vfxData != null) EditorGUILayout.PropertyField(vfxData, new GUIContent("VFX Settings"), true);
                    EditorGUI.indentLevel--;
                }

                // --- HÀNG 5: Event Effects ---
                SerializedProperty eventEffectsProp = windowRef.FindPropertyRelative("eventEffects");
                if (eventEffectsProp != null) EditorGUILayout.PropertyField(eventEffectsProp, true);

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }

            // Nút Add
            GUI.backgroundColor = new Color(0.5f, 1f, 0.5f);
            if (GUILayout.Button("+ Add New Action Window"))
            {
                windowsProp.arraySize++;
            }
            GUI.backgroundColor = Color.white;
            EditorGUI.indentLevel--;
        }

        // 🌟 BẮT BUỘC ĐỂ LƯU DỮ LIỆU: Áp dụng các thay đổi từ Inspector vào ScriptableObject gốc
        serializedObject.ApplyModifiedProperties();
    }

    private string[] AppendCustomOption(string[] presets)
    {
        string[] options = new string[presets.Length + 1];
        presets.CopyTo(options, 0);
        options[presets.Length] = "Custom...";
        return options;
    }
}
#endif