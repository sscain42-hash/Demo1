#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEditor.IMGUI.Controls;
using System.Linq;
using System.Collections.Generic;
using System0 = System.StringComparison;

public class AttackTimelineWindow : EditorWindow
{
    // --- DỮ LIỆU COMBO & ANIMATION ---
    private ComboSequence _targetComboSequence;
    private int _selectedAttackIndex = 0;
    private Animator _targetAnimator;
    private AnimationClip _selectedClip;

    // --- CHỌN WINDOW ĐỂ HIỆN CHI TIẾT ---
    private int _selectedWindowIndex = -1;

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

    // --- SCROLL POS ---
    private Vector2 _scrollPos;

    [MenuItem("Tools/Attack Timeline Window")]
    public static void OpenWindow()
    {
        AttackTimelineWindow window = GetWindow<AttackTimelineWindow>("Attack Timeline");
        window.minSize = new Vector2(650, 400);
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
            _selectedWindowIndex = -1; // Reset selection khi đổi combo
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
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        DrawHeaderToolbar();

        if (_targetComboSequence == null)
        {
            EditorGUILayout.HelpBox("Hãy chọn hoặc gán file ComboSequence ở thanh Toolbar để bắt đầu.", MessageType.Info);
            EditorGUILayout.EndScrollView();
            return;
        }

        if (TargetAttackData == null)
        {
            EditorGUILayout.HelpBox("ComboSequence hiện chưa có AttackData nào trong danh sách 'attacks'.", MessageType.Warning);
            EditorGUILayout.EndScrollView();
            return;
        }

        float totalFrames = GetTotalFrames();

        DrawPlaybackControls(totalFrames);
        EditorGUILayout.Space(5);
        DrawAddWindowToolbar();
        EditorGUILayout.Space(5);
        DrawTimelineTracks(totalFrames);

        EditorGUILayout.Space(10);
        DrawSelectedWindowDetails(); // 🔥 Vẽ bảng chi tiết Window được chọn

        EditorGUILayout.EndScrollView();
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
            _selectedWindowIndex = -1;
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
                _selectedWindowIndex = -1;
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
        if (GUILayout.Button("+ Step", EditorStyles.miniButtonMid, GUILayout.Width(60)))
        {
            AddNewWindow("Step");
        }

        // 🔥 NÚT MOTION+ NẰM Ở ĐÂY
        GUI.backgroundColor = new Color(0.4f, 1f, 0.4f);
        if (GUILayout.Button("Motion+", EditorStyles.miniButtonRight, GUILayout.Width(70)))
        {
            GenerateStepsFromRootMotion();
        }
        GUI.backgroundColor = Color.white;

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

            _selectedWindowIndex = windowsProp.arraySize - 1; // 🔥 Tự động select window mới tạo

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

            if (_selectedWindowIndex == index) _selectedWindowIndex = -1;
            else if (_selectedWindowIndex > index) _selectedWindowIndex--;

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
        // Giới hạn chiều cao để có thể scroll
        float height = Mathf.Max(150, TargetAttackData.windows.Count * 26f + 40);
        Rect contentRect = EditorGUILayout.GetControlRect(false, height);

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

                // 🔥 LÀM NỔI BẬT WINDOW ĐANG CHỌN
                if (i == _selectedWindowIndex)
                {
                    barColor = Color.Lerp(barColor, Color.white, 0.5f);
                    // Vẽ viền trắng
                    EditorGUI.DrawRect(new Rect(windowBarRect.x - 1, windowBarRect.y - 1, windowBarRect.width + 2, windowBarRect.height + 2), Color.white);
                }

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
                        _selectedWindowIndex = i; // 🔥 Select window
                        e.Use();
                    }
                    else if (rightHandleRect.Contains(e.mousePosition))
                    {
                        _currentDragMode = DragMode.EndTime;
                        _draggedWindowIndex = i;
                        _selectedWindowIndex = i; // 🔥 Select window
                        e.Use();
                    }
                    else if (windowBarRect.Contains(e.mousePosition))
                    {
                        _currentDragMode = DragMode.MoveWindow;
                        _draggedWindowIndex = i;
                        _selectedWindowIndex = i; // 🔥 Select window
                        float clickMouseNorm = (e.mousePosition.x - tracksRect.x) / tracksRect.width;
                        _dragOffsetNormalized = clickMouseNorm - startProp.floatValue;
                        e.Use();
                        Repaint(); // Vẽ lại để thấy viền trắng
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

    // =========================================================================
    // 🔥 BẢNG CHI TIẾT CỦA WINDOW ĐANG CHỌN
    // =========================================================================
    private void DrawSelectedWindowDetails()
    {
        AttackData currentAttack = TargetAttackData;
        if (currentAttack == null) return;

        SerializedObject serializedData = new SerializedObject(currentAttack);
        SerializedProperty windowsProp = serializedData.FindProperty("windows");

        if (_selectedWindowIndex < 0 || _selectedWindowIndex >= windowsProp.arraySize)
        {
            EditorGUILayout.HelpBox("Chọn một thanh Window trên Timeline để xem chi tiết.", MessageType.Info);
            return;
        }

        SerializedProperty windowProp = windowsProp.GetArrayElementAtIndex(_selectedWindowIndex);
        SerializedProperty nameProp = windowProp.FindPropertyRelative("actionName");

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField($"⚙️ DETAILS: {nameProp.stringValue} (Index {_selectedWindowIndex})", EditorStyles.boldLabel);

        serializedData.Update();

        // Hiển thị chung
        EditorGUILayout.PropertyField(nameProp, new GUIContent("Action Name"));

        SerializedProperty startProp = windowProp.FindPropertyRelative("startTime");
        SerializedProperty endProp = windowProp.FindPropertyRelative("endTime");

        // 🔥 SỬA LỖI CS0206: Dùng biến tạm thay vì truyền trực tiếp property
        float startVal = startProp.floatValue;
        float endVal = endProp.floatValue;

        EditorGUILayout.MinMaxSlider(new GUIContent("Time Range"), ref startVal, ref endVal, 0f, 1f);

        startProp.floatValue = startVal;
        endProp.floatValue = endVal;

        // Hiển thị riêng theo loại
        string actionName = nameProp.stringValue;
        if (actionName == "Step")
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.PropertyField(windowProp.FindPropertyRelative("cursorStep"), new GUIContent("Cursor Step"));
            EditorGUILayout.PropertyField(windowProp.FindPropertyRelative("targetDistance"), new GUIContent("Target Distance"));
            EditorGUILayout.PropertyField(windowProp.FindPropertyRelative("targetLayer"), new GUIContent("Target Layer (Chặn tường)"));
        }
        else if (actionName == "HitBox")
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.PropertyField(windowProp.FindPropertyRelative("hitBoxSize"), new GUIContent("HitBox Size"));
            EditorGUILayout.PropertyField(windowProp.FindPropertyRelative("hitBoxOffset"), new GUIContent("HitBox Offset"));
            EditorGUILayout.PropertyField(windowProp.FindPropertyRelative("targetLayer"), new GUIContent("Target Layer"));
        }

        EditorGUILayout.Space(5);
        EditorGUILayout.PropertyField(windowProp.FindPropertyRelative("enableVFX"), new GUIContent("Enable VFX"));
        if (windowProp.FindPropertyRelative("enableVFX").boolValue)
        {
            EditorGUILayout.PropertyField(windowProp.FindPropertyRelative("vfxTransform"), true);
        }

        EditorGUILayout.Space(5);
        // Chỉ hiện Event Effects nếu không phải là Step (theo yêu cầu của bạn)
        if (actionName != "Step")
        {
            EditorGUILayout.PropertyField(windowProp.FindPropertyRelative("eventEffects"), true);
        }

        serializedData.ApplyModifiedProperties();

        EditorGUILayout.EndVertical();
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
                _boxHandle.handleColor = (i == _selectedWindowIndex) ? Color.green : Color.yellow;
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

        var animator = Object.FindAnyObjectByType<Animator>();
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

    private void SampleAnimation()
    {
        AttackData currentAttack = TargetAttackData;

        if (_targetAnimator != null && currentAttack != null)
        {
            string stateName = !string.IsNullOrEmpty(currentAttack.animationName) ? currentAttack.animationName : currentAttack.name;

            if (!string.IsNullOrEmpty(stateName))
            {
                float totalFrames = GetTotalFrames();
                float normalizedTime = totalFrames > 0 ? Mathf.Clamp01(_currentFrame / totalFrames) : 0f;

                _targetAnimator.Play(stateName, 0, normalizedTime);
                _targetAnimator.Update(0f);
                SceneView.RepaintAll();
                return;
            }
        }

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

    // =========================================================================
    // 🔥 LOGIC CỦA NÚT MOTION+ (KHÔNG COPY EVENT EFFECTS)
    // =========================================================================
    private void GenerateStepsFromRootMotion()
    {
        AttackData currentAttack = TargetAttackData;
        if (currentAttack == null || _selectedClip == null)
        {
            Debug.LogWarning("[Motion+] Chưa chọn AttackData hoặc Animation Clip!");
            return;
        }

        GameObject targetGO = _targetAnimator != null ? _targetAnimator.gameObject : Selection.activeGameObject;
        if (targetGO == null)
        {
            Debug.LogWarning("[Motion+] Hãy chọn một GameObject (Player/Enemy) trong Scene để làm mẫu.");
            return;
        }

        EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(_selectedClip);
        AnimationCurve curveX = null, curveZ = null;

        foreach (var binding in bindings)
        {
            if (binding.path == "" && binding.propertyName == "RootT.x") curveX = AnimationUtility.GetEditorCurve(_selectedClip, binding);
            if (binding.path == "" && binding.propertyName == "RootT.z") curveZ = AnimationUtility.GetEditorCurve(_selectedClip, binding);
        }

        if (curveX == null && curveZ == null)
        {
            Debug.LogWarning($"[Motion+] Clip '{_selectedClip.name}' không có Root Position Keyframes. Hãy kiểm tra Import Settings.");
            return;
        }

        List<float> times = new List<float>();
        if (curveX != null) foreach (var key in curveX.keys) if (!times.Contains(key.time)) times.Add(key.time);
        if (curveZ != null) foreach (var key in curveZ.keys) if (!times.Contains(key.time)) times.Add(key.time);
        times.Sort();
        if (!times.Contains(0f)) times.Insert(0, 0f);
        if (!times.Contains(_selectedClip.length)) times.Add(_selectedClip.length);

        if (times.Count < 2) return;

        // Tính toán và gom cụm
        List<StepData> rawSteps = new List<StepData>();
        for (int i = 1; i < times.Count; i++)
        {
            float startTime = times[i - 1];
            float endTime = times[i];

            float x1 = curveX != null ? curveX.Evaluate(startTime) : 0f;
            float z1 = curveZ != null ? curveZ.Evaluate(startTime) : 0f;
            float x2 = curveX != null ? curveX.Evaluate(endTime) : 0f;
            float z2 = curveZ != null ? curveZ.Evaluate(endTime) : 0f;

            Vector3 startPos = new Vector3(x1, 0, z1);
            Vector3 endPos = new Vector3(x2, 0, z2);
            Vector3 delta = endPos - startPos;
            delta.y = 0;

            if (delta.magnitude < 0.005f) continue;

            rawSteps.Add(new StepData
            {
                startTime = startTime,
                endTime = endTime,
                delta = delta,
                speed = delta.magnitude / (endTime - startTime)
            });
        }

        if (rawSteps.Count == 0) return;

        List<StepData> mergedSteps = new List<StepData>();
        StepData currentGroup = rawSteps[0];
        float angleThreshold = 30f;
        float timeGapThreshold = 0.1f;

        for (int i = 1; i < rawSteps.Count; i++)
        {
            StepData nextStep = rawSteps[i];
            float angleDiff = Vector3.Angle(currentGroup.delta.normalized, nextStep.delta.normalized);
            float timeGap = nextStep.startTime - currentGroup.endTime;
            float speedDiff = Mathf.Abs(currentGroup.speed - nextStep.speed);

            if (angleDiff < angleThreshold && timeGap < timeGapThreshold && speedDiff < 2.0f)
            {
                currentGroup.endTime = nextStep.endTime;
                currentGroup.delta += nextStep.delta;
                currentGroup.speed = currentGroup.delta.magnitude / (currentGroup.endTime - currentGroup.startTime);
            }
            else
            {
                mergedSteps.Add(currentGroup);
                currentGroup = nextStep;
            }
        }
        mergedSteps.Add(currentGroup);

        SerializedObject serializedData = new SerializedObject(currentAttack);
        SerializedProperty windowsProp = serializedData.FindProperty("windows");
        serializedData.Update();

        // Xóa các Step cũ (nếu muốn) - hiện tại mình sẽ thêm vào cuối
        foreach (var step in mergedSteps)
        {
            windowsProp.arraySize++;
            SerializedProperty newElem = windowsProp.GetArrayElementAtIndex(windowsProp.arraySize - 1);

            // 🔥 CHỈ GÁN CÁC THUỘC TÍNH CẦN THIẾT, BỎ TRỐNG EVENT EFFECTS
            newElem.FindPropertyRelative("actionName").stringValue = "Step";
            newElem.FindPropertyRelative("startTime").floatValue = step.startTime / _selectedClip.length;
            newElem.FindPropertyRelative("endTime").floatValue = step.endTime / _selectedClip.length;
            newElem.FindPropertyRelative("cursorStep").boolValue = true;
            newElem.FindPropertyRelative("targetDistance").vector3Value = step.delta;

            SerializedProperty sizeProp = newElem.FindPropertyRelative("hitBoxSize");
            if (sizeProp != null) sizeProp.vector3Value = Vector3.one;
            SerializedProperty offsetProp = newElem.FindPropertyRelative("hitBoxOffset");
            if (offsetProp != null) offsetProp.vector3Value = Vector3.zero;

            // Xóa sạch Event Effects (để không copy Play Sound cũ)
            SerializedProperty eventEffectsProp = newElem.FindPropertyRelative("eventEffects");
            if (eventEffectsProp != null) eventEffectsProp.ClearArray();
        }

        serializedData.ApplyModifiedProperties();
        EditorUtility.SetDirty(currentAttack);

        Repaint();
        SceneView.RepaintAll();
        Debug.Log($"[Motion+] Đã tạo thành công {mergedSteps.Count} Step Windows (Không kèm Event).");
    }

    // Lớp phụ trợ cho thuật toán Gom nhóm
    private class StepData
    {
        public float startTime;
        public float endTime;
        public Vector3 delta;
        public float speed;
    }
}
#endif