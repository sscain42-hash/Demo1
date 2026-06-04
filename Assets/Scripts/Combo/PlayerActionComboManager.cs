using UnityEngine;
using System;
using System.Collections.Generic;

public class PlayerActionComboManager : MonoBehaviour
{
    [Header("Core Dependencies")]
    [SerializeField] private PlayerInputs playerInputs;
    private PlayerController _ctx;

    [Header("Combo Sequences")]
    [SerializeField] private ComboSequence normalAttackCombo; // Chuột trái
    [SerializeField] private ComboSequence skillECombo;        // Phím E
    [SerializeField] private ComboSequence skillQCombo;        // Phím Q

    // Lớp runtime quản lý trạng thái độc lập của từng chuỗi đòn đánh (Chuẩn Đóng Gói - Encapsulation)
    public class ComboState
    {
        public BufferedAction associatedAction;
        public ComboSequence sequence;
        public int currentIndex = 0;
        public bool isAttacking = false;
        public float comboResetTimer = 0f;
        public HashSet<string> activeWindows = new HashSet<string>();
    }

    private readonly ComboState stateNormal = new ComboState();
    private readonly ComboState stateE = new ComboState();
    private readonly ComboState stateQ = new ComboState();

    // State nào hiện tại đang chiếm quyền vung đòn hành động
    private ComboState _activeState = null;
    private AttackData _currentAttackData;
    private AttackType _currentRuntimeAttackType;

    // --- PROPERTIES CÔNG KHAI (Dành cho PlayerAttackState và Hệ thống trạng thái truy cập) ---
    public AttackData CurrentAttackData => _currentAttackData;
    public AttackType CurrentRuntimeAttackType => _currentRuntimeAttackType;
    public bool IsAttacking => _activeState != null && _activeState.isAttacking;

    // 🔥 CÁC CỜ TRẠNG THÁI MỚI ĐỒNG BỘ VỚI ACTION WINDOW HỆ THỐNG 🔥
    public bool IsComboWindowActive { get; private set; }
    public bool CanDashCancelNow { get; set; }
    public bool CanJumpCancelNow { get; set; }

    private void Awake()
    {
        _ctx = GetComponent<PlayerController>();
        if (playerInputs == null) playerInputs = GetComponent<PlayerInputs>();

        // Thiết lập liên kết dữ liệu với Buffer Enum (Tách biệt dữ liệu khởi tạo)
        InitializeComboStates();
    }

    private void InitializeComboStates()
    {
        stateNormal.associatedAction = BufferedAction.NormalAttack;
        stateNormal.sequence = normalAttackCombo;

        stateE.associatedAction = BufferedAction.ElementalSkill;
        stateE.sequence = skillECombo;

        stateQ.associatedAction = BufferedAction.ElementalBurst;
        stateQ.sequence = skillQCombo;
    }

    private void Update()
    {
        // 1. Cập nhật thời gian tự động gãy chuỗi combo nếu người chơi đứng im
        UpdateComboTimers(stateNormal);
        UpdateComboTimers(stateE);
        UpdateComboTimers(stateQ);

        // 2. Quét và đọc lệnh từ Input Buffer
        CheckAndProcessInputs();

        // 3. Quản lý các ô cửa sổ (Timeline) của đòn đánh đang kích hoạt
        if (IsAttacking)
        {
            UpdateComboWindows(_activeState);
        }
    }

    private void UpdateComboTimers(ComboState state)
    {
        if (state.sequence == null) return;

        if (state.comboResetTimer > 0 && !state.isAttacking)
        {
            state.comboResetTimer -= Time.deltaTime;
            if (state.comboResetTimer <= 0)
            {
                state.currentIndex = 0; // Quá thời gian bấm tiếp -> Reset về đòn đầu
            }
        }
    }

    private void CheckAndProcessInputs()
    {
        if (playerInputs == null) return;

        // TRƯỜNG HỢP 1: Nhân vật đang bình thường (Không có chuỗi nào đang chiếm quyền vung đòn)
        if (_activeState == null)
        {
            // Quét Buffer theo thứ tự ưu tiên lệnh từ hệ thống Input toàn cục
            if (_ctx.TryNormalAttack) { StartComboChain(stateNormal); return; }
            if (_ctx.TryElementalSkill) { StartComboChain(stateE); return; }
            if (_ctx.TryElementalBurst) { StartComboChain(stateQ); return; }
        }
        // TRƯỜNG HỢP 2: Nhân vật ĐANG VUNG ĐÒN và cửa sổ ComboInputBuffer đang mở ra
        else if (_activeState.isAttacking && IsComboWindowActive)
        {
            // Chỉ đọc lệnh trùng với loại chuỗi đang đánh để nối tiếp combo
            if (playerInputs.HasCommand(_activeState.associatedAction))
            {
                playerInputs.ConsumeCommand(_activeState.associatedAction); // Rút lệnh ra khỏi Buffer
                MoveToNextComboStep(_activeState);
            }
        }
    }

    private void StartComboChain(ComboState state)
    {
        if (state.sequence == null || state.sequence.attacks.Count == 0) return;

        // Rút lệnh ra khỏi bộ đệm đầu vào
        playerInputs.ConsumeCommand(state.associatedAction);
        _activeState = state;

        // KÍCH HOẠT CHUYỂN STATE: Ép State Machine cấp cao chuyển sang Root State Attack
        if (_ctx != null && _ctx.CurrentState != null)
        {
            _ctx.CurrentState.SwitchState(_ctx.States.Attack());
        }

        // Thực thi bước hoạt ảnh đầu tiên
        ExecuteComboStep(_activeState);
    }

    private void ExecuteComboStep(ComboState state)
    {
        state.isAttacking = true;
        _currentAttackData = state.sequence.attacks[state.currentIndex];

        // Xác định kiểu loại đòn đánh tại Runtime dựa trên Slot hành động
        _currentRuntimeAttackType = ConvertActionToAttackType(state.associatedAction);

        // Đóng/Mở các bộ điều khiển cửa sổ trạng thái về mặc định
        IsComboWindowActive = false;
        CanDashCancelNow = false;
        CanJumpCancelNow = false;

        // Đảm bảo các đối tượng dữ liệu ScriptableObject reset sạch sẽ trạng thái cũ
        foreach (var window in _currentAttackData.windows)
        {
            window.ResetRuntime();
        }
        state.activeWindows.Clear();

        // Kích hoạt phát hoạt ảnh đòn đánh tương ứng
        Animator anim = GetComponentInChildren<Animator>();
        if (anim != null)
        {
            anim.CrossFadeInFixedTime(_currentAttackData.animationName, 0.1f, 0, 0f);
        }
    }

    private void UpdateComboWindows(ComboState state)
    {
        Animator anim = GetComponentInChildren<Animator>();
        if (anim == null) return;

        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
        if (!stateInfo.IsName(_currentAttackData.animationName)) return;

        float nTime = stateInfo.normalizedTime;
        if (stateInfo.loop) nTime %= 1f;

        // Thiết lập biến tạm cục bộ cho khung hình để tránh lỗi lưu đè dữ liệu cũ
        bool currentFrameComboActive = false;
        bool currentFrameDashActive = false;
        bool currentFrameJumpActive = false;

        // Duyệt qua kiến trúc danh sách các ActionWindow cấu hình sẵn từ AttackData
        foreach (var window in _currentAttackData.windows)
        {
            bool isInside = window.IsInside(nTime);

            if (isInside)
            {
                // --- KÍCH HOẠT EVENT SCRIPTABLE OBJECT (CHỈ CHẠY ĐÚNG 1 LẦN KHI VÀO WINDOW) ---
                if (window.eventEffects != null && !window.eventTriggered)
                {
                    window.eventTriggered = true;
                    foreach (var effect in window.eventEffects)
                    {
                        if (effect != null) effect.Trigger(gameObject,window);
                    }
                }

                // --- XỬ LÝ LƯU TRỮ VÀ GÁN TRẠNG THÁI FEATURE MẶC ĐỊNH ---
                if (!state.activeWindows.Contains(window.actionName))
                {
                    state.activeWindows.Add(window.actionName);
                }

                // Phân tích văn bản định danh hệ thống dựa trên Custom Editor đã thiết lập
                switch (window.actionName)
                {
          
                    case "ComboInputBuffer":
                        currentFrameComboActive = true;
                        break;
                    case "DashCancel":
                        currentFrameDashActive = true;
                        break;
                    case "JumpCancel":
                        currentFrameJumpActive = true;
                        break;
                }
            }

            // --- THOÁT KHỎI WINDOW (DỌN DẸP TRẠNG THÁI RUNTIME) ---
            if (!isInside && state.activeWindows.Contains(window.actionName))
            {
                state.activeWindows.Remove(window.actionName);
                window.ResetRuntime();
            }
        }

        // Đồng bộ hóa trạng thái cuối cùng của khung hình vào Properties
        IsComboWindowActive = currentFrameComboActive;
        CanDashCancelNow = currentFrameDashActive;
        CanJumpCancelNow = currentFrameJumpActive;

        // BIỆN PHÁP AN TOÀN: Hoạt ảnh trôi về cuối đòn mà không có lệnh gối đầu nào tiếp theo
        if (nTime >= 0.95f)
        {
            FinishComboAttack(state);
        }
    }

    private void MoveToNextComboStep(ComboState state)
    {
        state.currentIndex++;
        if (state.currentIndex >= state.sequence.attacks.Count)
        {
            state.currentIndex = 0; // Hết chuỗi -> Quay về đòn 1
        }
        ExecuteComboStep(state);
    }

    private void FinishComboAttack(ComboState state)
    {
        state.isAttacking = false;
        state.activeWindows.Clear();
        state.comboResetTimer = 1.0f; // Chờ 1s để bấm tiếp giữ chuỗi, quá thời gian sẽ tự gãy combo

        if (state.currentIndex == state.sequence.attacks.Count - 1)
        {
            state.currentIndex = 0; // Đã đánh đến đòn cuối cùng của chuỗi
        }
        else
        {
            state.currentIndex++; // Tăng sẵn chỉ số index chuẩn bị cho lần bấm gối đầu sau
        }

        // Trả trạng thái tự do về rỗng để chuyển mạch State Machine
        IsComboWindowActive = false;
        CanDashCancelNow = false;
        CanJumpCancelNow = false;
        _activeState = null;
    }

    public void ForceCancelCombo()
    {
        if (_activeState != null)
        {
            _activeState.isAttacking = false;
            _activeState.activeWindows.Clear();
        }
        IsComboWindowActive = false;
        CanDashCancelNow = false;
        CanJumpCancelNow = false;
        _activeState = null;
        _currentAttackData = null;
    }

    private AttackType ConvertActionToAttackType(BufferedAction action)
    {
        return action switch
        {
            BufferedAction.NormalAttack => AttackType.NormalAttack,
            BufferedAction.ElementalSkill => AttackType.E,
            BufferedAction.ElementalBurst => AttackType.Q,
            _ => AttackType.NormalAttack
        };
    }
}