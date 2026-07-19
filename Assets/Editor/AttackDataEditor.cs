#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEditor.Animations;
using System.Linq;
using System.Reflection;

[CustomEditor(typeof(AttackData))]
public class AttackDataEditor : Editor
{
    private readonly string[] _actionPresets = { "ComboInputBuffer", "DashCancel", "JumpCancel", "Step" };
    private bool _showWindows = true;

    private static RuntimeAnimatorController _previewAnimator;
    private static int _selectedClipIndex = 0;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // 1. Vẽ các thuộc tính mặc định
        DrawPropertiesExcluding(serializedObject, "windows");

        EditorGUILayout.Space(10);

        // 2. Editor Helper (Animator Controller & Animation Window Integration)
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Editor Animator Helper", EditorStyles.boldLabel);
        _previewAnimator = (RuntimeAnimatorController)EditorGUILayout.ObjectField("Animator Controller", _previewAnimator, typeof(RuntimeAnimatorController), false);

        AnimationClip selectedClip = null;
        if (_previewAnimator is AnimatorController controller)
        {
            var clips = controller.animationClips;
            string[] clipNames = clips.Select(c => c.name).ToArray();

            EditorGUI.BeginChangeCheck();
            _selectedClipIndex = EditorGUILayout.Popup("Select Animation", _selectedClipIndex, clipNames);
            if (EditorGUI.EndChangeCheck())
            {
                selectedClip = clips[_selectedClipIndex];
                OpenAndFocusAnimationWindow(selectedClip);
            }

            if (_selectedClipIndex >= 0 && _selectedClipIndex < clips.Length)
                selectedClip = clips[_selectedClipIndex];
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);

        // 3. Action Windows
        SerializedProperty windowsProp = serializedObject.FindProperty("windows");
        _showWindows = EditorGUILayout.Foldout(_showWindows, $"⏱ Action Windows ({windowsProp.arraySize})", true, EditorStyles.foldoutHeader);

        if (_showWindows)
        {
            float totalFrames = (selectedClip != null) ? selectedClip.length * selectedClip.frameRate : 60f;

            EditorGUI.indentLevel++;
            for (int i = 0; i < windowsProp.arraySize; i++)
            {
                SerializedProperty windowRef = windowsProp.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // --- Header: Controls ---
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("▲", GUILayout.Width(20)) && i > 0) windowsProp.MoveArrayElement(i, i - 1);
                if (GUILayout.Button("▼", GUILayout.Width(20)) && i < windowsProp.arraySize - 1) windowsProp.MoveArrayElement(i, i + 1);

                // 🌟 ĐOẠN ĐÃ SỬA: Xử lý logic hiển thị Dropdown và ô TextBox Custom
                SerializedProperty nameProp = windowRef.FindPropertyRelative("actionName");
                int selectedIndex = System.Array.IndexOf(_actionPresets, nameProp.stringValue);
                int displayIndex = (selectedIndex == -1) ? _actionPresets.Length : selectedIndex;

                int newIndex = EditorGUILayout.Popup(displayIndex, AppendCustomOption(_actionPresets), GUILayout.Width(130));

                if (newIndex < _actionPresets.Length)
                {
                    nameProp.stringValue = _actionPresets[newIndex];
                }
                else
                {
                    if (selectedIndex != -1)
                    {
                        nameProp.stringValue = ""; // Giải phóng chuỗi cũ để mở ô nhập text
                    }
                }

                if (System.Array.IndexOf(_actionPresets, nameProp.stringValue) == -1)
                {
                    nameProp.stringValue = EditorGUILayout.TextField(nameProp.stringValue);
                }
                // ---------------------------------------------------------------------

                if (GUILayout.Button(EditorGUIUtility.IconContent("TreeEditor.Duplicate"), GUILayout.Width(30))) windowsProp.InsertArrayElementAtIndex(i);
                if (GUILayout.Button("✕", GUILayout.Width(25))) windowsProp.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndHorizontal();

                // --- Time Range (Slider hiển thị Frame) ---
                SerializedProperty startProp = windowRef.FindPropertyRelative("startTime");
                SerializedProperty endProp = windowRef.FindPropertyRelative("endTime");

                float startFrame = startProp.floatValue * totalFrames;
                float endFrame = endProp.floatValue * totalFrames;

                EditorGUILayout.LabelField($"Frame Range: {Mathf.RoundToInt(startFrame)} - {Mathf.RoundToInt(endFrame)}");

                float start = startFrame;
                float end = endFrame;
                EditorGUILayout.MinMaxSlider(ref start, ref end, 0f, totalFrames);

                startProp.floatValue = start / totalFrames;
                endProp.floatValue = end / totalFrames;

                // --- Movement & VFX ---
                EditorGUILayout.PropertyField(windowRef.FindPropertyRelative("targetDistance"));

                SerializedProperty enableVFXProp = windowRef.FindPropertyRelative("enableVFX");
                EditorGUILayout.PropertyField(enableVFXProp);

                if (enableVFXProp.boolValue)
                {
                    SerializedProperty vfxData = windowRef.FindPropertyRelative("vfxTransform");
                    if (GUILayout.Button("Copy from Selection", GUILayout.Height(20)) && Selection.activeGameObject != null)
                    {
                        Transform t = Selection.activeGameObject.transform;
                        vfxData.FindPropertyRelative("positionOffset").vector3Value = t.localPosition;
                        vfxData.FindPropertyRelative("rotationOffset").vector3Value = t.localEulerAngles;
                        vfxData.FindPropertyRelative("scale").vector3Value = t.localScale;
                    }
                    EditorGUILayout.PropertyField(vfxData, true);
                }

                EditorGUILayout.PropertyField(windowRef.FindPropertyRelative("eventEffects"), true);
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }

            if (GUILayout.Button("+ Add New Action Window")) windowsProp.arraySize++;
            EditorGUI.indentLevel--;
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void OpenAndFocusAnimationWindow(AnimationClip clip)
    {
        EditorWindow animWindow = EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.AnimationWindow"));
        animWindow.Show();
        animWindow.Focus();

        var field = animWindow.GetType().GetField("m_AnimEditor", BindingFlags.NonPublic | BindingFlags.Instance);
        var animEditor = field?.GetValue(animWindow);
        var stateField = animEditor?.GetType().GetField("m_State", BindingFlags.NonPublic | BindingFlags.Instance);
        var state = stateField?.GetValue(animEditor);

        var method = state?.GetType().GetMethod("set_activeAnimationClip", BindingFlags.Public | BindingFlags.Instance);
        method?.Invoke(state, new object[] { clip });
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