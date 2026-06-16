using UnityEngine;
using System.Collections.Generic;
using System;

public class PlayerActionComboManager : MonoBehaviour, IVelocityProvider,IComboCharacter
{
    [Header("Core Dependencies")]
    [SerializeField] private PlayerInputs playerInputs;
    private PlayerController _ctx;

    [Header("Combo Sequences")]
    [SerializeField] private ComboSequence normalAttackCombo;
    [SerializeField] private ComboSequence skillECombo;
    [SerializeField] private ComboSequence skillQCombo;

    // IVelocityProvider Implementation
    public Vector3 CurrentStepVelocity { get; private set; }
    public bool IsActive => IsAttacking && !_isForceCancelled;
    public int Priority => 1;
    // combo
    
    private void OnEnable()
    {
        var controller = GetComponent<PlayerController>();
        if (controller != null) controller.RegisterVelocityProvider(this);
        // Đăng ký lắng nghe sự kiện
        if (_targetLock != null)
        {
            _targetLock.OnTargetLocked += HandleTargetLocked;
        }
    }


    private void OnDisable()
    {
        var controller = GetComponent<PlayerController>();
        if (controller != null) controller.UnregisterVelocityProvider(this);
        // Hủy đăng ký để tránh memory leak
        if (_targetLock != null)
        {
            _targetLock.OnTargetLocked -= HandleTargetLocked;
        }
    }

    public Vector3 GetVelocityModifier() => CurrentStepVelocity;

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

    private ComboState _activeState = null;
    private AttackData _currentAttackData;
    private List<ComboState> _allStates;

    public AttackData CurrentAttackData => _currentAttackData;
    public bool IsAttacking => _activeState != null && _activeState.isAttacking;
    public AttackType CurrentRuntimeAttackType { get; private set; }
    public bool IsComboWindowActive { get; private set; }
    public bool CanDashCancelNow { get; private set; }
    public bool CanJumpCancelNow { get; private set; }
    private bool _isForceCancelled = false;

    private void Start()
    {
        if (_targetLock == null)
            _targetLock = GetComponent<TargetLockManager>();
    }
    private void Awake()
    {
        _ctx = GetComponent<PlayerController>();
        if (playerInputs == null) playerInputs = GetComponent<PlayerInputs>();

        stateNormal.associatedAction = BufferedAction.NormalAttack;
        stateNormal.sequence = normalAttackCombo;
        stateE.associatedAction = BufferedAction.ElementalSkill;
        stateE.sequence = skillECombo;
        stateQ.associatedAction = BufferedAction.ElementalBurst;
        stateQ.sequence = skillQCombo;
        _allStates = new List<ComboState> { stateNormal, stateE, stateQ };
    }

    private void Update()
    {
        UpdateComboTimers(stateNormal);
        UpdateComboTimers(stateE);
        UpdateComboTimers(stateQ);
        CheckAndProcessInputs();

        if (IsAttacking) UpdateComboWindows(_activeState);
    }

    private void UpdateComboTimers(ComboState state)
    {
        if (state.sequence == null) return;
        if (state.comboResetTimer > 0 && !state.isAttacking)
        {
            state.comboResetTimer -= Time.deltaTime;
            if (state.comboResetTimer <= 0) state.currentIndex = 0;
        }
    }

    private void CheckAndProcessInputs()
    {
        if (playerInputs == null) return;

        if (_activeState == null)
        {
            if (_ctx.TryNormalAttack) StartComboChain(stateNormal);
            else if (_ctx.TryElementalSkill) StartComboChain(stateE);
            else if (_ctx.TryElementalBurst) StartComboChain(stateQ);
        }
        else if (_activeState.isAttacking)
        {
            // 1. ƯU TIÊN KIỂM TRA NGẮT CHIÊU (DASH / JUMP CANCEL) TRƯỚC
            // Giả định bạn có khai báo BufferedAction.Dash và BufferedAction.Jump trong Enum
            if (CanDashCancelNow && playerInputs.HasCommand(BufferedAction.Dash))
            {
                playerInputs.ConsumeCommand(BufferedAction.Dash);
                ForceCancelCombo();

                // Ép Controller chuyển ngay sang State Dash
                if (_ctx.CurrentState != null) _ctx.CurrentState.SwitchState(_ctx.States.Dash());
                return; // Thoát hàm để không kiểm tra tiếp
            }

            if (CanJumpCancelNow && playerInputs.HasCommand(BufferedAction.Jump))
            {
                playerInputs.ConsumeCommand(BufferedAction.Jump);
                ForceCancelCombo();

                // Ép Controller chuyển ngay sang State Jump
                if (_ctx.CurrentState != null) _ctx.CurrentState.SwitchState(_ctx.States.Jump());
                return; // Thoát hàm để không kiểm tra tiếp
            }

            // 2. NẾU KHÔNG CÓ LỆNH NGẮT, MỚI KIỂM TRA ĐÁNH TIẾP
            if (IsComboWindowActive && playerInputs.HasCommand(_activeState.associatedAction))
            {
                playerInputs.ConsumeCommand(_activeState.associatedAction);
                MoveToNextComboStep(_activeState);
            }
        }
    }

    private void StartComboChain(ComboState state)
    {
      
        _isForceCancelled = false;
        if (state.sequence == null || state.sequence.attacks.Count == 0) return;
        playerInputs.ConsumeCommand(state.associatedAction);
        _activeState = state;
        if (_ctx != null && _ctx.CurrentState != null)
            _ctx.CurrentState.SwitchState(_ctx.States.Attack());
        ExecuteComboStep(_activeState);
    }

    private void ExecuteComboStep(ComboState state)
    {
        HandleTargetLocked(_targetLock.CurrentTarget);
        state.isAttacking = true;
        _currentAttackData = state.sequence.attacks[state.currentIndex];

        // CẬP NHẬT GIÁ TRỊ NÀY Ở ĐÂY:
        CurrentRuntimeAttackType = ConvertActionToAttackType(state.associatedAction);

        IsComboWindowActive = false;
        CurrentStepVelocity = Vector3.zero;

        foreach (var window in _currentAttackData.windows) window.ResetRuntime();
        state.activeWindows.Clear();

        Animator anim = GetComponentInChildren<Animator>();
        if (anim != null) anim.CrossFadeInFixedTime(_currentAttackData.animationName, 0.1f, 0, 0f);
    }

    private void UpdateComboWindows(ComboState state)
    {
        Animator anim = GetComponentInChildren<Animator>();
        if (anim == null) return;

        // Lấy thông tin trạng thái từ Animator
        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
        if (!stateInfo.IsName(_currentAttackData.animationName)) return;

        // LẤY SPEED TỪ ANIMATOR STATE:
        // stateInfo.speed lấy đúng giá trị Speed mà bạn đã set là 2 trong Inspector
        float animSpeed = stateInfo.speed;

        // Độ dài thực tế sau khi đã nhân tốc độ (Ví dụ: Animation gốc dài 1s, speed=2 -> dài còn 0.5s)
        float effectiveLength = stateInfo.length / Mathf.Max(animSpeed, 0.001f);

        float nTime = stateInfo.normalizedTime % 1f;
        Vector3 frameDisplacement = Vector3.zero;
        bool comboActive = false;

        CanDashCancelNow = false;
        CanJumpCancelNow = false;




        foreach (var window in _currentAttackData.windows)
        {
            if (window.IsInside(nTime))
            {
                // Tính thời gian window chiếm (giây) dựa trên tốc độ đã hiệu chỉnh
                float windowTime = (window.endTime - window.startTime) * effectiveLength;
                float duration = Mathf.Max(windowTime, 0.001f);

                // Tính vận tốc = Quãng đường / Thời gian Window thực tế
                Vector3 velocity = window.targetDistance / duration;

                // Dịch chuyển trong frame hiện tại
                frameDisplacement += velocity * Time.deltaTime;

                if (window.actionName == "ComboInputBuffer") comboActive = true;
                if (window.actionName == "DashCancel") CanDashCancelNow = true;
                if (window.actionName == "JumpCancel") CanJumpCancelNow = true;
                if (window.actionName == "ComboInputBuffer") comboActive = true;

                if (window.eventEffects != null && !window.eventTriggered) 
                { 
                    Debug.Log("Đang kích hoạt Event Effect...");
                    window.eventTriggered = true; 
                    foreach (var effect in window.eventEffects) 
                        effect?.Trigger(gameObject, window); 
                }
            }
        }

        IsComboWindowActive = comboActive;
        CurrentStepVelocity = transform.TransformDirection(frameDisplacement);

        if (nTime >= 0.95f) FinishComboAttack(state);
    }



    private void MoveToNextComboStep(ComboState state)
    {
        state.currentIndex = (state.currentIndex + 1) % state.sequence.attacks.Count;
        ExecuteComboStep(state);
    }

    private void FinishComboAttack(ComboState state)
    {
        state.isAttacking = false;
        state.comboResetTimer = 1.0f;
        state.currentIndex = 0; // Reset về đòn đầu tiên sau khi kết thúc chuỗi
        ForceCancelCombo();
    }

    public void ForceCancelCombo()
    {
        // Bật cờ ngắt điện
        _isForceCancelled = true;

        CurrentStepVelocity = Vector3.zero;
        _activeState = null;

        // Reset các state
        foreach (var state in _allStates)
        {
            state.isAttacking = false;
            state.activeWindows.Clear();
        }

        var anim = GetComponentInChildren<Animator>();
        if (anim != null) _ctx.AnimationHandler.PlayAnimation(_ctx.ID_Idle, 0.1f);
    }

    private AttackType ConvertActionToAttackType(BufferedAction action) => action switch
    {
        BufferedAction.NormalAttack => AttackType.NormalAttack,
        BufferedAction.ElementalSkill => AttackType.E,
        BufferedAction.ElementalBurst => AttackType.Q,
        _ => AttackType.NormalAttack
    };
    #region Target Lock Integration
    [SerializeField] private TargetLockManager _targetLock;


    private void HandleTargetLocked(GameObject target)
    {
        if (target == null) return;
        FaceTarget(target.transform);
    }

    private void FaceTarget(Transform target)
    {
        Vector3 direction = (target.position - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(direction);
    }
    #endregion
}