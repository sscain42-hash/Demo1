using UnityEngine;
using System.Collections.Generic;
using System;
using UnityEngine.Events;

public class PlayerSkillManager : MonoBehaviour, IVelocityProvider, IComboCharacter
{
    [Header("Core Dependencies")]
    [SerializeField] private PlayerInputs playerInputs;
    [SerializeField] private TargetLockManager targetLock;
    private PlayerController _ctx;
    private ComboEngine _comboEngine;

    [Header("Combo Sequences")]
    [SerializeField] private ComboSequence normalAttackCombo;
    [SerializeField] private ComboSequence skillECombo;
    [SerializeField] private ComboSequence skillQCombo;


    public class ComboState
    {
        public BufferedAction associatedAction;
        public ComboSequence sequence;
        public int currentIndex = 0;
        public bool isAttacking = false;
        public float comboResetTimer = 0f;
    }

    private readonly ComboState stateNormal = new ComboState();
    private readonly ComboState stateE = new ComboState();
    private readonly ComboState stateQ = new ComboState();
    private ComboState _activeState = null;
    private List<ComboState> _allStates;

    public AttackData CurrentAttackData => _comboEngine?.CurrentAttackData;
    public AttackType CurrentRuntimeAttackType { get; private set; }
    public Vector3 CurrentStepVelocity => _comboEngine != null ? _comboEngine.CurrentStepVelocity : Vector3.zero;
    public bool IsActive => IsAttacking && !_isForceCancelled;
    public int Priority => 1;
    public bool IsAttacking => _activeState != null && _activeState.isAttacking;

    private bool _isForceCancelled = false;
    public bool CanDashCancelNow => _comboEngine != null && _comboEngine.CanDashCancelNow;
    public bool CanJumpCancelNow => _comboEngine != null && _comboEngine.CanJumpCancelNow;

    private void OnEnable()
    {
        _ctx = GetComponent<PlayerController>();
        if (_ctx != null) _ctx.RegisterVelocityProvider(this);
        if (targetLock != null) targetLock.OnTargetLocked += HandleTargetLocked;
    }

    private void OnDisable()
    {
        if (_ctx != null) _ctx.UnregisterVelocityProvider(this);
        if (targetLock != null) targetLock.OnTargetLocked -= HandleTargetLocked;

    }

    public Vector3 GetVelocityModifier() => CurrentStepVelocity;

    private void Awake()
    {
        if (playerInputs == null) playerInputs = FindAnyObjectByType<PlayerInputs>();
        if (targetLock == null) targetLock = GetComponent<TargetLockManager>();

        stateNormal.associatedAction = BufferedAction.NormalAttack;
        stateNormal.sequence = normalAttackCombo;
        stateE.associatedAction = BufferedAction.ElementalSkill;
        stateE.sequence = skillECombo;
        stateQ.associatedAction = BufferedAction.ElementalBurst;
        stateQ.sequence = skillQCombo;
        _allStates = new List<ComboState> { stateNormal, stateE, stateQ };

        Animator anim = GetComponentInChildren<Animator>();
        _comboEngine = new ComboEngine(gameObject, anim, this);
    }

    private void Update()
    {
        UpdateComboTimers(stateNormal);
        UpdateComboTimers(stateE);
        UpdateComboTimers(stateQ);

        CheckAndProcessInputs();

        if (IsAttacking)
        {
            _comboEngine.UpdateWindows();

            // 🔥 SỬA TẠI ĐÂY: Chờ hoạt ảnh chạy gần như hết phim (98%) mới kết thúc logic tấn công
            if (_comboEngine.GetNormalizedTime() >= 0.98f)
                FinishComboAttack(_activeState);
        }
    }

    private void FinishComboAttack(ComboState state)
    {
        state.isAttacking = false;
        state.comboResetTimer = 1.0f;
        state.currentIndex = 0;

        // 🔥 SỬA TẠI ĐÂY: XÓA BỎ hàm ForceCancelCombo() ở đây! 
        // Nếu gọi ForceCancelCombo() tại đây, nó sẽ xóa sạch dữ liệu đòn đánh khiến Animator bị khựng.
        // Hãy để im cho State Machine (PlayerAttackState) tự thu hồi trạng thái.
        _activeState = null;
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
            if (_comboEngine.CanDashCancelNow && playerInputs.HasCommand(BufferedAction.Dash))
            {
                playerInputs.ConsumeCommand(BufferedAction.Dash);
                ForceCancelCombo();
                _ctx.CurrentState?.SwitchState(_ctx.States.Dash());
                return;
            }
            if (_comboEngine.CanJumpCancelNow && playerInputs.HasCommand(BufferedAction.Jump))
            {
                playerInputs.ConsumeCommand(BufferedAction.Jump);
                ForceCancelCombo();
                _ctx.CurrentState?.SwitchState(_ctx.States.Jump());
                return;
            }

            if (_comboEngine.IsComboWindowActive && playerInputs.HasCommand(_activeState.associatedAction))
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
        _ctx.CurrentState?.SwitchState(_ctx.States.Attack());

     
        ExecuteComboStep(_activeState);
    }

    private void ExecuteComboStep(ComboState state)
    {
        if (targetLock != null) HandleTargetLocked(targetLock.CurrentTarget);
        state.isAttacking = true;

        CurrentRuntimeAttackType = state.associatedAction switch
        {
            BufferedAction.NormalAttack => AttackType.NormalAttack,
            BufferedAction.ElementalSkill => AttackType.E,
            BufferedAction.ElementalBurst => AttackType.Q,
            _ => AttackType.NormalAttack
        };
        _ctx.RaiseSkillCast(CurrentRuntimeAttackType);
        _comboEngine.ChangeAttackData(state.sequence.attacks[state.currentIndex]);
    }

    private void MoveToNextComboStep(ComboState state)
    {
        state.currentIndex = (state.currentIndex + 1) % state.sequence.attacks.Count;

      

        ExecuteComboStep(state);
    }


    /// <summary>
    /// 🔥 Hàm hủy toàn bộ đăng ký (Unregister) thủ công khi không dùng nữa để tránh rác bộ nhớ
    /// </summary>



    public void ForceCancelCombo()
    {
        _isForceCancelled = true;
        _activeState = null;

        foreach (var state in _allStates) state.isAttacking = false;
        _comboEngine.ChangeAttackData(null);

        // Chỉ ép phát hoạt ảnh Idle lập tức khi bị hủy đòn chủ động
        _ctx.AnimationHandler?.PlayAnimation(_ctx.ID_Idle, 0.1f);
    }

    private void HandleTargetLocked(GameObject target)
    {
        if (target == null) return;
        Vector3 direction = (target.transform.position - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero) transform.rotation = Quaternion.LookRotation(direction);
    }


}