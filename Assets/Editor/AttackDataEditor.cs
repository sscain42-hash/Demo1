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
        DrawPropertiesExcluding(serializedObject, "windows");

        EditorGUILayout.Space(10);
        SerializedProperty windowsProp = serializedObject.FindProperty("windows");

        _showWindows = EditorGUILayout.Foldout(_showWindows, $"⏱ Action Windows ({windowsProp.arraySize})", true, EditorStyles.foldoutHeader);
        
        if (_showWindows)
        {
            EditorGUI.indentLevel++;
            
            for (int i = 0; i < windowsProp.arraySize; i++)
            {
                SerializedProperty windowRef = windowsProp.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // --- HÀNG 1: Điều hướng & Xóa ---
                EditorGUILayout.BeginHorizontal();
                
                // Nút Di chuyển (Sử dụng GUIUtility.ExitGUI để ép vẽ lại)
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

                int newIndex = EditorGUILayout.Popup(selectedIndex == -1 ? _actionPresets.Length : selectedIndex, AppendCustomOption(_actionPresets));
                if (newIndex < _actionPresets.Length) nameProp.stringValue = _actionPresets[newIndex];
                else EditorGUILayout.PropertyField(nameProp, GUIContent.none);

                // Nút Xóa
                GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
                if (GUILayout.Button("✕", GUILayout.Width(25))) 
                { 
                    windowsProp.DeleteArrayElementAtIndex(i);
                    serializedObject.ApplyModifiedProperties();
                    GUIUtility.ExitGUI(); 
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();

                // --- SLIDER THỜI GIAN ---
                SerializedProperty startProp = windowRef.FindPropertyRelative("startTime");
                SerializedProperty endProp = windowRef.FindPropertyRelative("endTime");
                float start = startProp.floatValue;
                float end = endProp.floatValue;
                EditorGUILayout.MinMaxSlider(new GUIContent("Time Range"), ref start, ref end, 0f, 1f);
                startProp.floatValue = start;
                endProp.floatValue = end;

                // --- VFX CONFIG ---
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
                            Debug.Log("Copied VFX Transform from: " + t.name);
                        }
                    }
                    EditorGUILayout.PropertyField(vfxData, new GUIContent("VFX Settings"), true);
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.PropertyField(windowRef.FindPropertyRelative("eventEffects"), true);
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }

            // Nút Add
            EditorGUILayout.Space(5);
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