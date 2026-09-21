#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEditor.Animations;
using UnityEditor.IMGUI.Controls;
using System.Linq;
using System.Reflection;

[CustomEditor(typeof(AttackData))]
public class AttackDataEditor : Editor
{
    private readonly string[] _actionPresets = { "ComboInputBuffer", "DashCancel", "JumpCancel", "Step", "HitBox" };
    private bool _showWindows = true;
    private bool _showTimeline = true;

    private static RuntimeAnimatorController _previewAnimator;
    private static int _selectedClipIndex = 0;

    private static float _currentScrubberFrame = 0f;
    private bool _isDraggingScrubber = false;

    private readonly BoxBoundsHandle _boxHandle = new BoxBoundsHandle();

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        FetchAnimatorFromSelection();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.RepaintAll();
    }

    private void OnSelectionChange()
    {
        FetchAnimatorFromSelection();
        Repaint();
    }

    private void FetchAnimatorFromSelection()
    {
        GameObject selectedGO = Selection.activeGameObject;
        if (selectedGO != null)
        {
            Animator anim = selectedGO.GetComponentInParent<Animator>();
            if (anim != null && anim.runtimeAnimatorController != null)
            {
                _previewAnimator = anim.runtimeAnimatorController;
            }
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // 1. Vẽ thuộc tính mặc định ngoại trừ danh sách windows
        DrawPropertiesExcluding(serializedObject, "windows");

        EditorGUILayout.Space(10);

        // 2. Editor Animator Helper
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Editor Animator Helper", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        _previewAnimator = (RuntimeAnimatorController)EditorGUILayout.ObjectField("Animator Controller", _previewAnimator, typeof(RuntimeAnimatorController), false);

        if (GUILayout.Button("Get From Selected", EditorStyles.miniButton, GUILayout.Width(120)))
        {
            FetchAnimatorFromSelection();
        }
        EditorGUILayout.EndHorizontal();

        AnimationClip selectedClip = null;
        if (_previewAnimator is AnimatorController controller)
        {
            var clips = controller.animationClips;
            string[] clipNames = clips.Select(c => c.name).ToArray();

            EditorGUI.BeginChangeCheck();
            _selectedClipIndex = EditorGUILayout.Popup("Select Animation", _selectedClipIndex, clipNames);
            if (EditorGUI.EndChangeCheck())
            {
                if (_selectedClipIndex >= 0 && _selectedClipIndex < clips.Length)
                {
                    selectedClip = clips[_selectedClipIndex];
                    OpenAndFocusAnimationWindow(selectedClip);
                }
            }

            if (_selectedClipIndex >= 0 && _selectedClipIndex < clips.Length)
                selectedClip = clips[_selectedClipIndex];
        }
        else
        {
            EditorGUILayout.HelpBox("Chọn GameObject có Animator trong Hierarchy để tự động liên kết Clip.", MessageType.Info);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);

        SerializedProperty windowsProp = serializedObject.FindProperty("windows");
        float totalFrames = (selectedClip != null) ? selectedClip.length * selectedClip.frameRate : 60f;

        // 3. TIMELINE SCRUBBER (Thanh tua thời gian chuẩn Unity)
        _showTimeline = EditorGUILayout.Foldout(_showTimeline, "🎬 Animation Timeline Scrubber", true, EditorStyles.foldoutHeader);
        if (_showTimeline)
        {
            DrawTimelineScrubber(windowsProp, totalFrames, selectedClip);
        }

        EditorGUILayout.Space(10);

        // 4. ACTION WINDOWS DETAIL
        _showWindows = EditorGUILayout.Foldout(_showWindows, $"⏱ Action Windows Detail ({windowsProp.arraySize})", true, EditorStyles.foldoutHeader);

        if (_showWindows)
        {
            EditorGUI.indentLevel++;
            for (int i = 0; i < windowsProp.arraySize; i++)
            {
                SerializedProperty windowRef = windowsProp.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // --- Header Controls ---
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("▲", GUILayout.Width(20)) && i > 0) windowsProp.MoveArrayElement(i, i - 1);
                if (GUILayout.Button("▼", GUILayout.Width(20)) && i < windowsProp.arraySize - 1) windowsProp.MoveArrayElement(i, i + 1);

                SerializedProperty nameProp = windowRef.FindPropertyRelative("actionName");
                int selectedIndex = System.Array.IndexOf(_actionPresets, nameProp.stringValue);
                int displayIndex = (selectedIndex == -1) ? _actionPresets.Length : selectedIndex;

                int newIndex = EditorGUILayout.Popup(displayIndex, AppendCustomOption(_actionPresets), GUILayout.Width(130));

                if (newIndex < _actionPresets.Length)
                {
                    nameProp.stringValue = _actionPresets[newIndex];
                }
                else if (selectedIndex != -1)
                {
                    nameProp.stringValue = "";
                }

                if (System.Array.IndexOf(_actionPresets, nameProp.stringValue) == -1)
                {
                    nameProp.stringValue = EditorGUILayout.TextField(nameProp.stringValue);
                }

                if (GUILayout.Button(EditorGUIUtility.IconContent("TreeEditor.Duplicate"), GUILayout.Width(30)))
                {
                    windowsProp.InsertArrayElementAtIndex(i);
                }

                if (GUILayout.Button("✕", GUILayout.Width(25)))
                {
                    int oldSize = windowsProp.arraySize;
                    windowsProp.DeleteArrayElementAtIndex(i);
                    if (windowsProp.arraySize == oldSize) windowsProp.DeleteArrayElementAtIndex(i);

                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }
                EditorGUILayout.EndHorizontal();

                // --- Time Range ---
                SerializedProperty startProp = windowRef.FindPropertyRelative("startTime");
                SerializedProperty endProp = windowRef.FindPropertyRelative("endTime");

                float startFrame = startProp.floatValue * totalFrames;
                float endFrame = endProp.floatValue * totalFrames;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Frame Range: {Mathf.RoundToInt(startFrame)} - {Mathf.RoundToInt(endFrame)}", EditorStyles.boldLabel);

                if (GUILayout.Button("Sync Frame to Scrubber", EditorStyles.miniButton, GUILayout.Width(150)))
                {
                    _currentScrubberFrame = startFrame;
                    if (selectedClip != null) SampleAnimationAtFrame(selectedClip, _currentScrubberFrame);
                }
                EditorGUILayout.EndHorizontal();

                float start = startFrame;
                float end = endFrame;
                EditorGUILayout.MinMaxSlider(ref start, ref end, 0f, totalFrames);

                startProp.floatValue = Mathf.Clamp(start / totalFrames, 0f, 1f);
                endProp.floatValue = Mathf.Clamp(end / totalFrames, 0f, 1f);

                // --- Action Specific Settings ---
                string currentAction = nameProp.stringValue;

                if (currentAction == "Step")
                {
                    EditorGUILayout.Space(2);
                    EditorGUILayout.LabelField("Step Movement Settings", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(windowRef.FindPropertyRelative("cursorStep"), new GUIContent("Cursor Step"));
                    EditorGUILayout.PropertyField(windowRef.FindPropertyRelative("targetDistance"), new GUIContent("Target Distance"));
                }
                else if (currentAction == "HitBox")
                {
                    EditorGUILayout.Space(2);
                    EditorGUILayout.LabelField("HitBox Settings", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(windowRef.FindPropertyRelative("hitBoxSize"), new GUIContent("HitBox Size"));
                    EditorGUILayout.PropertyField(windowRef.FindPropertyRelative("hitBoxOffset"), new GUIContent("HitBox Offset"));
                    EditorGUILayout.PropertyField(windowRef.FindPropertyRelative("targetLayer"), new GUIContent("Target Layer"));
                }

                // --- VFX Settings ---
                EditorGUILayout.Space(2);
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

                // --- Event Effects ---
                EditorGUILayout.PropertyField(windowRef.FindPropertyRelative("eventEffects"), true);

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }

            // Thêm mới Window và khởi tạo giá trị mặc định để lưu dữ liệu
            if (GUILayout.Button("+ Add New Action Window", GUILayout.Height(25)))
            {
                windowsProp.arraySize++;
                SerializedProperty newElem = windowsProp.GetArrayElementAtIndex(windowsProp.arraySize - 1);

                newElem.FindPropertyRelative("actionName").stringValue = "HitBox";
                newElem.FindPropertyRelative("startTime").floatValue = 0f;
                newElem.FindPropertyRelative("endTime").floatValue = 0.5f;
                newElem.FindPropertyRelative("hitBoxSize").vector3Value = Vector3.one;
                newElem.FindPropertyRelative("hitBoxOffset").vector3Value = Vector3.zero;
            }
            EditorGUI.indentLevel--;
        }

        serializedObject.ApplyModifiedProperties();
    }

    // --- TIMELINE & SCRUBBER GUI ---
    private void DrawTimelineScrubber(SerializedProperty windowsProp, float totalFrames, AnimationClip clip)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Current Frame: {Mathf.RoundToInt(_currentScrubberFrame)} / {Mathf.RoundToInt(totalFrames)}", EditorStyles.boldLabel);
        if (GUILayout.Button("🔄 Sync Selected Frame", EditorStyles.miniButton, GUILayout.Width(150)) && clip != null)
        {
            SampleAnimationAtFrame(clip, _currentScrubberFrame);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // Khung hình Timeline
        Rect timelineRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(32), GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(timelineRect, new Color(0.18f, 0.18f, 0.18f, 1f));

        // Vẽ vạch chia Frame
        Handles.color = new Color(0.5f, 0.5f, 0.5f, 0.4f);
        int step = Mathf.Max(1, Mathf.RoundToInt(totalFrames / 10f));
        for (int f = 0; f <= totalFrames; f += step)
        {
            float x = timelineRect.x + (f / totalFrames) * timelineRect.width;
            Handles.DrawLine(new Vector3(x, timelineRect.yMax - 8), new Vector3(x, timelineRect.yMax));
            GUI.Label(new Rect(x - 10, timelineRect.y + 2, 25, 14), f.ToString(), EditorStyles.miniLabel);
        }

        // Vẽ các thanh Action Windows (Dải màu Cyan)
        for (int i = 0; i < windowsProp.arraySize; i++)
        {
            SerializedProperty windowRef = windowsProp.GetArrayElementAtIndex(i);
            SerializedProperty startProp = windowRef.FindPropertyRelative("startTime");
            SerializedProperty endProp = windowRef.FindPropertyRelative("endTime");

            float barX = timelineRect.x + startProp.floatValue * timelineRect.width;
            float barWidth = Mathf.Max(2f, (endProp.floatValue - startProp.floatValue) * timelineRect.width);
            Rect miniBar = new Rect(barX, timelineRect.yMax - 6, barWidth, 4);
            EditorGUI.DrawRect(miniBar, Color.cyan);
        }

        // Vẽ kim tua Timeline màu đỏ
        _currentScrubberFrame = Mathf.Clamp(_currentScrubberFrame, 0f, totalFrames);
        float scrubberX = timelineRect.x + (_currentScrubberFrame / totalFrames) * timelineRect.width;

        Rect handleHead = new Rect(scrubberX - 6, timelineRect.y, 12, 12);
        EditorGUI.DrawRect(new Rect(scrubberX - 1, timelineRect.y, 2, timelineRect.height), Color.red);
        EditorGUI.DrawRect(handleHead, Color.red);

        EditorGUIUtility.AddCursorRect(timelineRect, MouseCursor.ResizeHorizontal);

        // Xử lý kéo/thả kim tua Timeline
        UnityEngine.Event currentEvent = UnityEngine.Event.current;
        if (currentEvent.type == EventType.MouseDown && timelineRect.Contains(currentEvent.mousePosition))
        {
            _isDraggingScrubber = true;
            UpdateScrubberFromMouse(currentEvent.mousePosition, timelineRect, totalFrames, clip);
            currentEvent.Use();
        }
        else if (currentEvent.type == EventType.MouseDrag && _isDraggingScrubber)
        {
            UpdateScrubberFromMouse(currentEvent.mousePosition, timelineRect, totalFrames, clip);
            currentEvent.Use();
        }
        else if (currentEvent.type == EventType.MouseUp)
        {
            _isDraggingScrubber = false;
        }

        EditorGUILayout.EndVertical();
    }

    private void UpdateScrubberFromMouse(Vector2 mousePos, Rect timelineRect, float totalFrames, AnimationClip clip)
    {
        float normalizedX = Mathf.Clamp01((mousePos.x - timelineRect.x) / timelineRect.width);
        _currentScrubberFrame = normalizedX * totalFrames;

        if (clip != null)
        {
            SampleAnimationAtFrame(clip, _currentScrubberFrame);
        }

        Repaint(); // Vẽ lại Inspector ngay lập tức để chuyển động mượt mà
        GUI.changed = true;
    }

    // --- VẼ VÀ ĐIỀU CHỈNH HITBOX BẰNG SCENEGUI ---
    private void OnSceneGUI(SceneView sceneView)
    {
        serializedObject.Update();

        SerializedProperty windowsProp = serializedObject.FindProperty("windows");
        if (windowsProp == null || !windowsProp.isArray) return;

        Transform originTransform = GetTargetTransform();
        if (originTransform == null) return;

        AnimationClip currentClip = GetCurrentClip();
        float totalFrames = (currentClip != null) ? currentClip.length * currentClip.frameRate : 60f;
        float normalizedCurrentFrame = _currentScrubberFrame / totalFrames;

        for (int i = 0; i < windowsProp.arraySize; i++)
        {
            SerializedProperty windowProp = windowsProp.GetArrayElementAtIndex(i);
            SerializedProperty actionNameProp = windowProp.FindPropertyRelative("actionName");

            if (actionNameProp.stringValue == "HitBox")
            {
                SerializedProperty startProp = windowProp.FindPropertyRelative("startTime");
                SerializedProperty endProp = windowProp.FindPropertyRelative("endTime");

                bool isActiveFrame = (normalizedCurrentFrame >= startProp.floatValue && normalizedCurrentFrame <= endProp.floatValue);
                if (!isActiveFrame) continue;

                SerializedProperty sizeProp = windowProp.FindPropertyRelative("hitBoxSize");
                SerializedProperty offsetProp = windowProp.FindPropertyRelative("hitBoxOffset");

                Vector3 currentSize = (sizeProp.vector3Value == Vector3.zero) ? Vector3.one : sizeProp.vector3Value;
                Vector3 worldCenter = originTransform.TransformPoint(offsetProp.vector3Value);

                // Box Bounds Handle
                Handles.matrix = Matrix4x4.TRS(worldCenter, originTransform.rotation, Vector3.one);
                _boxHandle.center = Vector3.zero;
                _boxHandle.size = currentSize;
                _boxHandle.handleColor = Color.yellow;
                _boxHandle.wireframeColor = Color.cyan;

                EditorGUI.BeginChangeCheck();
                _boxHandle.DrawHandle();
                if (EditorGUI.EndChangeCheck())
                {
                    sizeProp.vector3Value = _boxHandle.size;

                    if (_boxHandle.center != Vector3.zero)
                    {
                        Vector3 worldShift = originTransform.rotation * _boxHandle.center;
                        offsetProp.vector3Value += originTransform.InverseTransformVector(worldShift);
                    }
                }

                // Position Handle
                Handles.matrix = Matrix4x4.identity;
                EditorGUI.BeginChangeCheck();
                Vector3 newWorldCenter = Handles.PositionHandle(worldCenter, originTransform.rotation);
                if (EditorGUI.EndChangeCheck())
                {
                    offsetProp.vector3Value = originTransform.InverseTransformPoint(newWorldCenter);
                }

                Handles.Label(worldCenter + Vector3.up * (_boxHandle.size.y * 0.5f + 0.2f),
                    "HitBox (ACTIVE)",
                    new GUIStyle()
                    {
                        normal = new GUIStyleState() { textColor = Color.red },
                        alignment = TextAnchor.MiddleCenter,
                        fontStyle = FontStyle.Bold
                    });
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    private Transform GetTargetTransform()
    {
        if (Selection.activeTransform != null) return Selection.activeTransform;

        var animator = FindFirstObjectByType<Animator>();
        return animator != null ? animator.transform : null;
    }

    private AnimationClip GetCurrentClip()
    {
        if (_previewAnimator is AnimatorController controller)
        {
            var clips = controller.animationClips;
            if (_selectedClipIndex >= 0 && _selectedClipIndex < clips.Length)
                return clips[_selectedClipIndex];
        }
        return null;
    }

    private void SampleAnimationAtFrame(AnimationClip clip, float frame)
    {
        GameObject targetGo = Selection.activeGameObject;
        if (targetGo == null)
        {
            var animator = FindFirstObjectByType<Animator>();
            if (animator != null) targetGo = animator.gameObject;
        }

        if (targetGo == null || clip == null) return;

        float time = frame / clip.frameRate;
        clip.SampleAnimation(targetGo, time);
        SceneView.RepaintAll();
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
