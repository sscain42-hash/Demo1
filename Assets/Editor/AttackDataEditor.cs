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
        serializedObject.Update();

        // 1. Vẽ các thuộc tính gốc (như animationName, v.v.)
        DrawPropertiesExcluding(serializedObject, "windows");

        EditorGUILayout.Space(10);
        SerializedProperty windowsProp = serializedObject.FindProperty("windows");

        // 2. Foldout quản lý danh sách Windows
        _showWindows = EditorGUILayout.Foldout(_showWindows, $"⏱ Action Windows ({windowsProp.arraySize})", true, EditorStyles.foldoutHeader);

        if (_showWindows)
        {
            EditorGUI.indentLevel++;

            for (int i = 0; i < windowsProp.arraySize; i++)
            {
                SerializedProperty windowRef = windowsProp.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // --- HÀNG 1: Điều hướng & Tên Action & Xóa ---
                EditorGUILayout.BeginHorizontal();
                
                if (GUILayout.Button("▲", GUILayout.Width(20)) && i > 0) 
                { windowsProp.MoveArrayElement(i, i - 1); serializedObject.ApplyModifiedProperties(); GUIUtility.ExitGUI(); }
                
                if (GUILayout.Button("▼", GUILayout.Width(20)) && i < windowsProp.arraySize - 1) 
                { windowsProp.MoveArrayElement(i, i + 1); serializedObject.ApplyModifiedProperties(); GUIUtility.ExitGUI(); }

                SerializedProperty nameProp = windowRef.FindPropertyRelative("actionName");
                
                // Chọn Preset
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

                GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
                if (GUILayout.Button("✕", GUILayout.Width(25))) 
                { 
                    windowsProp.DeleteArrayElementAtIndex(i); 
                    serializedObject.ApplyModifiedProperties(); 
                    GUIUtility.ExitGUI(); 
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();

                // --- HÀNG 2: Time Range Slider (Tự động tính thời gian di chuyển) ---
                SerializedProperty startProp = windowRef.FindPropertyRelative("startTime");
                SerializedProperty endProp = windowRef.FindPropertyRelative("endTime");
                float start = startProp.floatValue;
                float end = endProp.floatValue;
                
                EditorGUILayout.MinMaxSlider(new GUIContent("Time Range"), ref start, ref end, 0f, 1f);
                startProp.floatValue = start;
                endProp.floatValue = end;

                // --- HÀNG 3: Target Distance (Đã bỏ duration) ---
                EditorGUILayout.PropertyField(windowRef.FindPropertyRelative("targetDistance"), new GUIContent("Target Distance (Local)"));

                // --- HÀNG 4: VFX Config (Khôi phục feature cũ) ---
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
                    EditorGUILayout.PropertyField(vfxData, new GUIContent("VFX Settings"), true);
                    EditorGUI.indentLevel--;
                }

                // --- HÀNG 5: Event Effects (Khôi phục feature cũ) ---
                EditorGUILayout.PropertyField(windowRef.FindPropertyRelative("eventEffects"), true);

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