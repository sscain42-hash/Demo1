#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using System.Linq;
using System0 = System.StringComparison;

public class AttackTimelineWindow : EditorWindow
{
    // --- DỮ LIỆU COMBO & ANIMATION ---
    private ComboSequence _targetComboSequence;
    private int _selectedAttackIndex = 0;
    private Animator _targetAnimator;
    private AnimationClip _selectedClip;

    private AttackData TargetAttackData
    {
        get
        {
            if (_targetComboSequence != null && _targetComboSequence.attacks != null &&
                _selectedAttackIndex >= 0 && _selectedAttackIndex < _targetComboSequence.attacks.Count)
            {
                return _targetComboSequence.attacks[_selectedAttackIndex];
            }
            return null;
        }
    }

    // --- TIMELINE & PLAYBACK ---
    private float _currentFrame = 0f;
    private bool _isPlaying = false;
    private bool _snapToFrame = true;
    private double _lastEditorTime;

    // --- KÉO THẢ (DRAG) ---
    private enum DragMode { None, StartTime, EndTime, MoveWindow }
    private DragMode _currentDragMode = DragMode.None;
    private int _draggedWindowIndex = -1;
    private float _dragOffsetNormalized = 0f;

    // --- CUSTOM WINDOW NAME INPUT ---
    private string _customActionName = "CustomAction";

    // --- SCENE GUI HANDLE ---
    private readonly BoxBoundsHandle _boxHandle = new BoxBoundsHandle();

    [MenuItem("Tools/Attack Timeline Window")]
    public static void OpenWindow()
    {
        AttackTimelineWindow window = GetWindow<AttackTimelineWindow>("Attack Timeline");
        window.minSize = new Vector2(650, 300);
        window.Show();
    }

    private void OnEnable()
    {
        _lastEditorTime = EditorApplication.timeSinceStartup;
        EditorApplication.update += OnEditorUpdate;
        Selection.selectionChanged += OnSelectionChanged;

        SceneView.duringSceneGui += OnSceneGUI;
        AutoFetchSelection();
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
        Selection.selectionChanged -= OnSelectionChanged;
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void OnSelectionChanged()
    {
        AutoFetchSelection();
        Repaint();
    }

    private void AutoFetchSelection()
    {
        if (Selection.activeObject is ComboSequence combo)
        {
            _targetComboSequence = combo;
            UpdateSelectedClip();
        }

        if (Selection.activeGameObject != null)
        {
            Animator anim = Selection.activeGameObject.GetComponentInParent<Animator>();
            if (anim != null)
            {
                _targetAnimator = anim;
                UpdateSelectedClip();
            }
        }
    }

    private void OnGUI()
    {
        DrawHeaderToolbar();

        if (_targetComboSequence == null)
        {
            EditorGUILayout.HelpBox("Hãy chọn hoặc gán file ComboSequence ở thanh Toolbar để bắt đầu.", MessageType.Info);
            return;
        }

        if (TargetAttackData == null)
        {
            EditorGUILayout.HelpBox("ComboSequence hiện chưa có AttackData nào trong danh sách 'attacks'.", MessageType.Warning);
            return;
        }

        float totalFrames = GetTotalFrames();

        DrawPlaybackControls(totalFrames);
        EditorGUILayout.Space(5);
        DrawAddWindowToolbar();
        EditorGUILayout.Space(5);
        DrawTimelineTracks(totalFrames);
    }

    private float GetTotalFrames()
    {
        if (_selectedClip == null || _selectedClip.frameRate <= 0) return 60f;
        return Mathf.Max(1f, _selectedClip.length * _selectedClip.frameRate);
    }

    private void DrawHeaderToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        EditorGUI.BeginChangeCheck();
        _targetComboSequence = (ComboSequence)EditorGUILayout.ObjectField("Combo Sequence", _targetComboSequence, typeof(ComboSequence), false, GUILayout.Width(250));
        if (EditorGUI.EndChangeCheck())
        {
            _selectedAttackIndex = 0;
            UpdateSelectedClip();
        }

        EditorGUI.BeginChangeCheck();
        _targetAnimator = (Animator)EditorGUILayout.ObjectField("Animator", _targetAnimator, typeof(Animator), true, GUILayout.Width(220));
        if (EditorGUI.EndChangeCheck())
        {
            UpdateSelectedClip();
        }

        if (_targetComboSequence != null && _targetComboSequence.attacks != null && _targetComboSequence.attacks.Count > 0)
        {
            string[] popupOptions = new string[_targetComboSequence.attacks.Count];
            for (int i = 0; i < _targetComboSequence.attacks.Count; i++)
            {
                AttackData attack = _targetComboSequence.attacks[i];
                popupOptions[i] = (attack != null) ? $"[{i + 1}] {attack.name} ({attack.animationName})" : $"[{i + 1}] Unassigned AttackData";
            }

            EditorGUI.BeginChangeCheck();
            _selectedAttackIndex = EditorGUILayout.Popup(_selectedAttackIndex, popupOptions, EditorStyles.toolbarPopup, GUILayout.Width(200));
            if (EditorGUI.EndChangeCheck())
            {
                UpdateSelectedClip();
            }
        }

        GUILayout.FlexibleSpace();

        _snapToFrame = GUILayout.Toggle(_snapToFrame, "Snap Frame", EditorStyles.toolbarButton, GUILayout.Width(80));

        EditorGUILayout.EndHorizontal();
    }

    private void DrawAddWindowToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

        EditorGUILayout.LabelField("Add Window:", EditorStyles.boldLabel, GUILayout.Width(85));

        if (GUILayout.Button("+ HitBox", EditorStyles.miniButtonLeft, GUILayout.Width(80)))
        {
            AddNewWindow("HitBox");
        }
        if (GUILayout.Button("+ DashCancel", EditorStyles.miniButtonMid, GUILayout.Width(85)))
        {
            AddNewWindow("DashCancel");
        }
        if (GUILayout.Button("+ JumpCancel", EditorStyles.miniButtonMid, GUILayout.Width(85)))
        {
            AddNewWindow("JumpCancel");
        }
        if (GUILayout.Button("+ Step", EditorStyles.miniButtonRight, GUILayout.Width(60)))
        {
            AddNewWindow("Step");
        }

        EditorGUILayout.Space(10);
        _customActionName = EditorGUILayout.TextField(_customActionName, GUILayout.Width(110));
        if (GUILayout.Button("+ Custom", EditorStyles.miniButton, GUILayout.Width(65)))
        {
            if (!string.IsNullOrEmpty(_customActionName))
            {
                AddNewWindow(_customActionName);
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    private void AddNewWindow(string actionName)
    {
        AttackData currentAttack = TargetAttackData;
        if (currentAttack == null) return;

        SerializedObject serializedData = new SerializedObject(currentAttack);
        SerializedProperty windowsProp = serializedData.FindProperty("windows");

        if (windowsProp != null && windowsProp.isArray)
        {
            serializedData.Update();
            windowsProp.arraySize++;
            SerializedProperty newWindowProp = windowsProp.GetArrayElementAtIndex(windowsProp.arraySize - 1);

            newWindowProp.FindPropertyRelative("actionName").stringValue = actionName;
            newWindowProp.FindPropertyRelative("startTime").floatValue = 0.2f;
            newWindowProp.FindPropertyRelative("endTime").floatValue = 0.5f;

            SerializedProperty sizeProp = newWindowProp.FindPropertyRelative("hitBoxSize");
            if (sizeProp != null) sizeProp.vector3Value = Vector3.one;

            SerializedProperty offsetProp = newWindowProp.FindPropertyRelative("hitBoxOffset");
            if (offsetProp != null) offsetProp.vector3Value = Vector3.zero;

            serializedData.ApplyModifiedProperties();
            EditorUtility.SetDirty(currentAttack);

            Repaint();
            SceneView.RepaintAll();
        }
    }

    private void RemoveWindow(int index)
    {
        AttackData currentAttack = TargetAttackData;
        if (currentAttack == null) return;

        SerializedObject serializedObject = new SerializedObject(currentAttack);
        SerializedProperty windowsProp = serializedObject.FindProperty("windows");

        if (windowsProp != null && windowsProp.isArray && index >= 0 && index < windowsProp.arraySize)
        {
            serializedObject.Update();
            windowsProp.DeleteArrayElementAtIndex(index);
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(currentAttack);

            Repaint();
            SceneView.RepaintAll();
        }
    }

    private void UpdateSelectedClip()
    {
        AttackData currentAttack = TargetAttackData;
        _selectedClip = null;

        if (currentAttack != null)
        {
            if (_targetAnimator != null && _targetAnimator.runtimeAnimatorController != null)
            {
                var controller = _targetAnimator.runtimeAnimatorController;
                if (controller.animationClips != null)
                {
                    string targetAnimName = currentAttack.animationName;

                    _selectedClip = controller.animationClips.FirstOrDefault(c =>
                        c != null && !string.IsNullOrEmpty(targetAnimName) &&
                        (c.name.Equals(targetAnimName, System0.OrdinalIgnoreCase) ||
                         c.name.Equals(currentAttack.name, System0.OrdinalIgnoreCase)));
                }
            }

            if (_selectedClip == null && !string.IsNullOrEmpty(currentAttack.animationName))
            {
                string[] guids = AssetDatabase.FindAssets($"{currentAttack.animationName} t:AnimationClip");
                if (guids.Length > 0)
                {
                    foreach (var guid in guids)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        var loadedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                        if (loadedClip != null && loadedClip.name.Equals(currentAttack.animationName, System0.OrdinalIgnoreCase))
                        {
                            _selectedClip = loadedClip;
                            break;
                        }
                    }
                }
            }
        }

        _currentFrame = 0f;
        SampleAnimation();
    }

    private void DrawPlaybackControls(float totalFrames)
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

        string playIcon = _isPlaying ? "PauseButton" : "PlayButton";
        if (GUILayout.Button(EditorGUIUtility.IconContent(playIcon), GUILayout.Width(35), GUILayout.Height(22)))
        {
            _isPlaying = !_isPlaying;
            _lastEditorTime = EditorApplication.timeSinceStartup;
        }

        if (GUILayout.Button(EditorGUIUtility.IconContent("BeginningButton"), GUILayout.Width(30), GUILayout.Height(22)))
        {
            _currentFrame = 0f;
            SampleAnimation();
        }

        if (GUILayout.Button(EditorGUIUtility.IconContent("EndButton"), GUILayout.Width(30), GUILayout.Height(22)))
        {
            _currentFrame = totalFrames;
            SampleAnimation();
        }

        EditorGUILayout.Space(10);

        string animInfo = (_selectedClip != null) ? $"Clip: {_selectedClip.name} ({_selectedClip.frameRate} FPS)" : "Clip: None (Not Loaded)";
        EditorGUILayout.LabelField($"Frame: {Mathf.RoundToInt(_currentFrame)} / {Mathf.RoundToInt(totalFrames)} | {animInfo}", EditorStyles.boldLabel);

        EditorGUILayout.EndHorizontal();
    }

    private void DrawTimelineTracks(float totalFrames)
    {
        Rect contentRect = EditorGUILayout.GetControlRect(false, GUILayout.ExpandHeight(true));
        if (contentRect.height < 100) contentRect.height = 100;

        const float deleteButtonWidth = 22f;
        Rect timelineAreaRect = new Rect(contentRect.x + deleteButtonWidth, contentRect.y, contentRect.width - deleteButtonWidth, contentRect.height);

        // 1. Thước đo Frame (Ruler)
        Rect rulerRect = new Rect(timelineAreaRect.x, timelineAreaRect.y, timelineAreaRect.width, 24);
        EditorGUI.DrawRect(rulerRect, new Color(0.2f, 0.2f, 0.2f, 1f));

        Handles.color = new Color(0.6f, 0.6f, 0.6f, 0.6f);
        int step = Mathf.Max(1, Mathf.RoundToInt(totalFrames / 15f));
        for (int f = 0; f <= totalFrames; f += step)
        {
            float x = rulerRect.x + (f / totalFrames) * rulerRect.width;
            Handles.DrawLine(new Vector3(x, rulerRect.yMax - 8), new Vector3(x, rulerRect.yMax));
            GUI.Label(new Rect(x - 10, rulerRect.y, 30, 15), f.ToString(), EditorStyles.miniLabel);
        }

        // 2. Tracks
        Rect tracksRect = new Rect(timelineAreaRect.x, rulerRect.yMax, timelineAreaRect.width, timelineAreaRect.height - rulerRect.height);
        EditorGUI.DrawRect(tracksRect, new Color(0.14f, 0.14f, 0.14f, 1f));

        AttackData currentAttack = TargetAttackData;
        if (currentAttack == null) return;

        SerializedObject serializedData = new SerializedObject(currentAttack);
        SerializedProperty windowsProp = serializedData.FindProperty("windows");

        UnityEngine.Event e = UnityEngine.Event.current;

        if (windowsProp != null && windowsProp.isArray)
        {
            float trackHeight = 26f;
            const float handleWidth = 6f;

            for (int i = 0; i < windowsProp.arraySize; i++)
            {
                SerializedProperty window = windowsProp.GetArrayElementAtIndex(i);
                SerializedProperty nameProp = window.FindPropertyRelative("actionName");
                SerializedProperty startProp = window.FindPropertyRelative("startTime");
                SerializedProperty endProp = window.FindPropertyRelative("endTime");

                float trackY = tracksRect.y + (i * trackHeight);

                // Nút Xóa Track (X)
                Rect deleteBtnRect = new Rect(contentRect.x, trackY + 2, deleteButtonWidth - 2, trackHeight - 4);
                if (GUI.Button(deleteBtnRect, "X", EditorStyles.miniButton))
                {
                    RemoveWindow(i);
                    return;
                }

                float barX = tracksRect.x + startProp.floatValue * tracksRect.width;
                float barWidth = Mathf.Max(8f, (endProp.floatValue - startProp.floatValue) * tracksRect.width);
                Rect windowBarRect = new Rect(barX, trackY + 2, barWidth, trackHeight - 6);

                Rect leftHandleRect = new Rect(barX - (handleWidth / 2f), trackY + 2, handleWidth, trackHeight - 6);
                Rect rightHandleRect = new Rect(barX + barWidth - (handleWidth / 2f), trackY + 2, handleWidth, trackHeight - 6);

                Color barColor = GetColorForAction(nameProp.stringValue);
                EditorGUI.DrawRect(windowBarRect, barColor);
                EditorGUI.DrawRect(leftHandleRect, Color.white);
                EditorGUI.DrawRect(rightHandleRect, Color.white);

                int startF = Mathf.RoundToInt(startProp.floatValue * totalFrames);
                int endF = Mathf.RoundToInt(endProp.floatValue * totalFrames);
                GUI.Label(windowBarRect, $"  {nameProp.stringValue} [{startF}F - {endF}F]", EditorStyles.whiteMiniLabel);

                EditorGUIUtility.AddCursorRect(leftHandleRect, MouseCursor.ResizeHorizontal);
                EditorGUIUtility.AddCursorRect(rightHandleRect, MouseCursor.ResizeHorizontal);
                EditorGUIUtility.AddCursorRect(windowBarRect, MouseCursor.MoveArrow);

                // Mouse Down
                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    if (leftHandleRect.Contains(e.mousePosition))
                    {
                        _currentDragMode = DragMode.StartTime;
                        _draggedWindowIndex = i;
                        e.Use();
                    }
                    else if (rightHandleRect.Contains(e.mousePosition))
                    {
                        _currentDragMode = DragMode.EndTime;
                        _draggedWindowIndex = i;
                        e.Use();
                    }
                    else if (windowBarRect.Contains(e.mousePosition))
                    {
                        _currentDragMode = DragMode.MoveWindow;
                        _draggedWindowIndex = i;
                        float clickMouseNorm = (e.mousePosition.x - tracksRect.x) / tracksRect.width;
                        _dragOffsetNormalized = clickMouseNorm - startProp.floatValue;
                        e.Use();
                    }
                }
            }

            // Mouse Drag
            if (e.type == EventType.MouseDrag && _draggedWindowIndex >= 0 && _draggedWindowIndex < windowsProp.arraySize)
            {
                SerializedProperty window = windowsProp.GetArrayElementAtIndex(_draggedWindowIndex);
                SerializedProperty startProp = window.FindPropertyRelative("startTime");
                SerializedProperty endProp = window.FindPropertyRelative("endTime");

                float currentMouseNorm = Mathf.Clamp01((e.mousePosition.x - tracksRect.x) / tracksRect.width);
                float minStep = 1f / Mathf.Max(1f, totalFrames);

                switch (_currentDragMode)
                {
                    case DragMode.StartTime:
                        float newStart = SnapNormalized(currentMouseNorm, totalFrames);
                        startProp.floatValue = Mathf.Min(newStart, endProp.floatValue - minStep);
                        break;

                    case DragMode.EndTime:
                        float newEnd = SnapNormalized(currentMouseNorm, totalFrames);
                        endProp.floatValue = Mathf.Max(newEnd, startProp.floatValue + minStep);
                        break;

                    case DragMode.MoveWindow:
                        float duration = endProp.floatValue - startProp.floatValue;
                        float rawStart = currentMouseNorm - _dragOffsetNormalized;
                        float snappedStart = SnapNormalized(rawStart, totalFrames);

                        snappedStart = Mathf.Clamp(snappedStart, 0f, 1f - duration);
                        startProp.floatValue = snappedStart;
                        endProp.floatValue = snappedStart + duration;
                        break;
                }

                serializedData.ApplyModifiedProperties();
                EditorUtility.SetDirty(currentAttack);

                e.Use();
                Repaint();
                SceneView.RepaintAll();
            }

            if (e.type == EventType.MouseUp)
            {
                _currentDragMode = DragMode.None;
                _draggedWindowIndex = -1;
            }
        }

        // 3. Kim tua Scrubber
        _currentFrame = Mathf.Clamp(_currentFrame, 0f, totalFrames);
        float scrubberX = timelineAreaRect.x + (_currentFrame / Mathf.Max(1f, totalFrames)) * timelineAreaRect.width;

        EditorGUI.DrawRect(new Rect(scrubberX - 1, timelineAreaRect.y, 2, timelineAreaRect.height), Color.red);
        EditorGUI.DrawRect(new Rect(scrubberX - 5, timelineAreaRect.y, 10, 14), Color.red);

        if (_currentDragMode == DragMode.None)
        {
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && rulerRect.Contains(e.mousePosition))
            {
                float normalizedX = Mathf.Clamp01((e.mousePosition.x - timelineAreaRect.x) / timelineAreaRect.width);
                float rawFrame = normalizedX * totalFrames;

                _currentFrame = _snapToFrame ? Mathf.Round(rawFrame) : rawFrame;
                SampleAnimation();
                e.Use();
                Repaint();
            }
        }
    }

    private float SnapNormalized(float normValue, float totalFrames)
    {
        if (!_snapToFrame || totalFrames <= 0) return normValue;

        float snappedFrame = Mathf.Round(normValue * totalFrames);
        return Mathf.Clamp01(snappedFrame / totalFrames);
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        AttackData currentAttack = TargetAttackData;
        if (currentAttack == null) return;

        Transform originTransform = GetTargetTransform();
        if (originTransform == null) return;

        float totalFrames = GetTotalFrames();

        SerializedObject serializedData = new SerializedObject(currentAttack);
        SerializedProperty windowsProp = serializedData.FindProperty("windows");

        if (windowsProp == null || !windowsProp.isArray) return;

        bool isModified = false;

        for (int i = 0; i < windowsProp.arraySize; i++)
        {
            SerializedProperty windowProp = windowsProp.GetArrayElementAtIndex(i);
            SerializedProperty actionNameProp = windowProp.FindPropertyRelative("actionName");

            if (actionNameProp.stringValue == "HitBox")
            {
                SerializedProperty startProp = windowProp.FindPropertyRelative("startTime");
                SerializedProperty endProp = windowProp.FindPropertyRelative("endTime");

                float startFrame = startProp.floatValue * totalFrames;
                float endFrame = endProp.floatValue * totalFrames;

                bool isActiveFrame = (_currentFrame >= startFrame - 0.05f && _currentFrame <= endFrame + 0.05f);
                if (!isActiveFrame) continue;

                SerializedProperty sizeProp = windowProp.FindPropertyRelative("hitBoxSize");
                SerializedProperty offsetProp = windowProp.FindPropertyRelative("hitBoxOffset");

                Vector3 currentSize = (sizeProp.vector3Value == Vector3.zero) ? Vector3.one : sizeProp.vector3Value;
                Vector3 worldCenter = originTransform.TransformPoint(offsetProp.vector3Value);

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
                    isModified = true;
                }

                Handles.matrix = Matrix4x4.identity;
                EditorGUI.BeginChangeCheck();
                Vector3 newWorldCenter = Handles.PositionHandle(worldCenter, originTransform.rotation);
                if (EditorGUI.EndChangeCheck())
                {
                    offsetProp.vector3Value = originTransform.InverseTransformPoint(newWorldCenter);
                    isModified = true;
                }

                int curF = Mathf.RoundToInt(_currentFrame);
                int sF = Mathf.RoundToInt(startFrame);
                int eF = Mathf.RoundToInt(endFrame);

                Handles.Label(worldCenter + Vector3.up * (_boxHandle.size.y * 0.5f + 0.2f),
                    $"HitBox ({currentAttack.name})\nActive: {sF}F - {eF}F (Cur: {curF}F)",
                    new GUIStyle()
                    {
                        normal = new GUIStyleState() { textColor = Color.red },
                        alignment = TextAnchor.MiddleCenter,
                        fontStyle = FontStyle.Bold
                    });
            }
        }

        if (isModified)
        {
            serializedData.ApplyModifiedProperties();
            EditorUtility.SetDirty(currentAttack);
            Repaint();
        }
    }

    private Transform GetTargetTransform()
    {
        if (_targetAnimator != null) return _targetAnimator.transform;
        if (Selection.activeTransform != null) return Selection.activeTransform;

        var animator = Object.FindFirstObjectByType<Animator>();
        return animator != null ? animator.transform : null;
    }

    private void OnEditorUpdate()
    {
        if (_isPlaying && _selectedClip != null)
        {
            double deltaTime = EditorApplication.timeSinceStartup - _lastEditorTime;
            _lastEditorTime = EditorApplication.timeSinceStartup;

            float totalFrames = GetTotalFrames();
            float frameRate = _selectedClip.frameRate > 0 ? _selectedClip.frameRate : 30f;
            _currentFrame += (float)deltaTime * frameRate;

            if (_currentFrame > totalFrames)
            {
                _currentFrame = 0f;
            }

            SampleAnimation();
            Repaint();
        }
    }

    // --- CẬP NHẬT LOGIC SAMPLE BẰNG ANIMATOR STATE ---
    private void SampleAnimation()
    {
        AttackData currentAttack = TargetAttackData;

        // Ưu tiên play bằng Animator State nếu có TargetAnimator
        if (_targetAnimator != null && currentAttack != null)
        {
            string stateName = !string.IsNullOrEmpty(currentAttack.animationName) ? currentAttack.animationName : currentAttack.name;

            if (!string.IsNullOrEmpty(stateName))
            {
                float totalFrames = GetTotalFrames();
                float normalizedTime = totalFrames > 0 ? Mathf.Clamp01(_currentFrame / totalFrames) : 0f;

                // Play Animator State ở layer 0 tại tỉ lệ thời gian (0.0 đến 1.0)
                _targetAnimator.Play(stateName, 0, normalizedTime);
                _targetAnimator.Update(0f); // Ép Animator cập nhật ngay trong Editor
                SceneView.RepaintAll();
                return;
            }
        }

        // Fallback: Sample trực tiếp Clip nếu không tìm thấy/chưa gán Animator
        if (_selectedClip != null)
        {
            GameObject targetGO = (_targetAnimator != null) ? _targetAnimator.gameObject : Selection.activeGameObject;
            if (targetGO != null)
            {
                float frameRate = _selectedClip.frameRate > 0 ? _selectedClip.frameRate : 30f;
                float time = _currentFrame / frameRate;
                time = Mathf.Clamp(time, 0f, _selectedClip.length);
                _selectedClip.SampleAnimation(targetGO, time);
                SceneView.RepaintAll();
            }
        }
    }

    private Color GetColorForAction(string actionName)
    {
        switch (actionName)
        {
            case "HitBox": return new Color(0.8f, 0.2f, 0.2f, 0.85f);
            case "DashCancel": return new Color(0.2f, 0.7f, 0.3f, 0.85f);
            case "JumpCancel": return new Color(0.2f, 0.5f, 0.9f, 0.85f);
            case "Step": return new Color(0.9f, 0.6f, 0.1f, 0.85f);
            default: return new Color(0.2f, 0.7f, 0.8f, 0.85f);
        }
    }
}
#endif