using UnityEngine;
using System.Collections.Generic;

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
        HandleCamera();
        CheckAndProcessInputs();

        if (IsAttacking)
        {
            _comboEngine.UpdateWindows();

            if (_comboEngine.GetNormalizedTime() >= 0.98f)
            {
                FinishComboAttack(_activeState);
            }
        }
    }

    private void FinishComboAttack(ComboState state)
    {
        state.isAttacking = false;
        state.comboResetTimer = 1.0f;
        _activeState = null;
    }

    private void UpdateComboTimers(ComboState state)
    {
        if (state.sequence == null) return;
        if (state.comboResetTimer > 0 && !state.isAttacking)
        {
            state.comboResetTimer -= Time.unscaledDeltaTime;
            if (state.comboResetTimer <= 0) state.currentIndex = 0;
        }
    }

    private void CheckAndProcessInputs()
    {
        if (playerInputs == null) return;

        if (_activeState == null)
        {
            if (_ctx.TryNormalAttack || playerInputs.HasCommand(BufferedAction.NormalAttack)) StartComboChain(stateNormal);
            else if (_ctx.TryElementalSkill || playerInputs.HasCommand(BufferedAction.ElementalSkill)) StartComboChain(stateE);
            else if (_ctx.TryElementalBurst || playerInputs.HasCommand(BufferedAction.ElementalBurst)) StartComboChain(stateQ);
        }
        else if (_activeState.isAttacking)
        {
            // Hủy đòn bằng Dash
            if (_comboEngine.CanDashCancelNow && playerInputs.HasCommand(BufferedAction.Dash))
            {
                playerInputs.ConsumeCommand(BufferedAction.Dash);
                ForceCancelCombo(false); // 🔥 SỬA: Đổi thành false để đồng bộ chặn Idle frame
                _ctx.CurrentState?.SwitchState(_ctx.States.Dash());
                return;
            }

            // Hủy đòn bằng Jump
            if (_comboEngine.CanJumpCancelNow && playerInputs.HasCommand(BufferedAction.Jump))
            {
                playerInputs.ConsumeCommand(BufferedAction.Jump);
                ForceCancelCombo(false); // 🔥 SỬA: Đổi thành false để đồng bộ chặn Idle frame
                _ctx.CurrentState?.SwitchState(_ctx.States.Jump());
                return;
            }

            if (_comboEngine.IsComboWindowActive)
            {
                if (playerInputs.HasCommand(BufferedAction.ElementalBurst))
                {
                    StartComboChain(stateQ);
                    return;
                }

                if (playerInputs.HasCommand(BufferedAction.ElementalSkill))
                {
                    StartComboChain(stateE);
                    return;
                }

                if (playerInputs.HasCommand(BufferedAction.NormalAttack))
                {
                    if (_activeState == stateNormal)
                    {
                        playerInputs.ConsumeCommand(BufferedAction.NormalAttack);
                        MoveToNextComboStep(stateNormal);
                    }
                    else
                    {
                        StartComboChain(stateNormal);
                    }
                    return;
                }
            }
        }
    }

    private void StartComboChain(ComboState state)
    {
        _isForceCancelled = false;
        if (state.sequence == null || state.sequence.attacks.Count == 0) return;

        if (_activeState != null && _activeState != state)
        {
            _activeState.isAttacking = false;
            _activeState.comboResetTimer = 1.0f;
        }

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

        if (state.currentIndex >= state.sequence.attacks.Count) state.currentIndex = 0;

        _comboEngine.ChangeAttackData(state.sequence.attacks[state.currentIndex]);
      
    }

    private void HandleCamera()
    {
        if (CurrentAttackData != null && CurrentAttackData.enableCameraZoom)
        {

            SkillCameraZoom.Instance?.ZoomIn();
        }
        else
        {
            SkillCameraZoom.Instance?.ZoomOut();

        }
    }

    private void MoveToNextComboStep(ComboState state)
    {
        state.currentIndex = (state.currentIndex + 1) % state.sequence.attacks.Count;
        ExecuteComboStep(state);
    }

    /// <summary>
    /// Hàm hủy chiêu chủ động (Khi dính Dash/Jump Cancel hoặc bị quái đánh ngắt)
    /// </summary>
    public void ForceCancelCombo(bool playIdleAnimation = true)
    {
        _isForceCancelled = true;
        _activeState = null;

        // 🔥 CHỐT CHẶN 1: Reset toàn bộ chỉ số Combo index về đòn đầu tiên (0) và xóa sạch bộ đếm thời gian
        foreach (var state in _allStates)
        {
            state.isAttacking = false;
            state.currentIndex = 0;     // Đưa tiến trình combo về đòn 1
            state.comboResetTimer = 0f;  // Xóa thời gian chờ phục hồi
        }

        // 🔥 CHỐT CHẶN 2: Nuốt (Clear) toàn bộ các lệnh tấn công cũ còn kẹt trong Input Buffer 
        // để tránh việc hệ thống tự động kích hoạt lại chiêu ở frame tiếp theo
        if (playerInputs != null)
        {
            playerInputs.ConsumeCommand(BufferedAction.NormalAttack);
            playerInputs.ConsumeCommand(BufferedAction.ElementalSkill);
            playerInputs.ConsumeCommand(BufferedAction.ElementalBurst);
        }

        _comboEngine.ChangeAttackData(null);
        if (playIdleAnimation)
        {
            _ctx.AnimationHandler?.PlayAnimation(_ctx.ID_Idle, 0.2f);
        }


    }

    private void HandleTargetLocked(GameObject target)
    {
        if (target == null) return;
        Vector3 direction = (target.transform.position - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero) transform.rotation = Quaternion.LookRotation(direction);
    }
}