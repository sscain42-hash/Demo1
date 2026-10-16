using UnityEngine;
using System.Collections.Generic;
using System;
using Combat.Parry;

[DefaultExecutionOrder(-200)]
public class PlayerSkillManager : MonoBehaviour, IVelocityProvider, IComboCharacter
{
    [Header("Core Dependencies")]
    [SerializeField] private PlayerInputs playerInputs;
    [SerializeField] private TargetLockManager targetLock;
    private PlayerController _ctx;
    private PlayerComboEngine _comboEngine;

    [Header("Combo Sequences")]
    [SerializeField] private ComboSequence normalAttackCombo;
    [SerializeField] private ComboSequence skillECombo;
    [SerializeField] private ComboSequence skillQCombo;

    [Header("Parry Sequence (Animation)")]
    [SerializeField] private ComboSequence parryCombo;

    // =========================================================
    // 🔥 COMBO TYPE — quyết định combo nào sẽ chạy khi vào AttackState
    // =========================================================
    public enum ComboType { Normal, SkillE, BurstQ }
    private ComboType _pendingComboType = ComboType.Normal;

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
    public float CurrrentProgressAnimation => _comboEngine != null ? _comboEngine.GetNormalizedTime() : 0f;
    private bool _isForceCancelled = false;
    public bool CanDashCancelNow => _comboEngine != null && _comboEngine.CanDashCancelNow;
    public bool CanJumpCancelNow => _comboEngine != null && _comboEngine.CanJumpCancelNow && _ctx.IsGroundedStable;

    public bool IsParryActive { get; private set; }

    private float _parryAnimationTimer = 0f;
    private float _parryAnimationDuration = 0f;
    private bool _isPlayingParryAnimation = false;

    public event Action OnAttackRequested;
    public event Action OnDashCancelRequested;
    public event Action OnJumpCancelRequested;

    private bool _eventsHooked = false;

    private void OnEnable()
    {
        if (_ctx != null) _ctx.RegisterVelocityProvider(this);
        if (targetLock != null) targetLock.OnTargetLocked += HandleTargetLocked;
    }

    private void Start()
    {
        HookParryEvents();
    }

    private void OnDisable()
    {
        if (_ctx != null) _ctx.UnregisterVelocityProvider(this);
        if (targetLock != null) targetLock.OnTargetLocked -= HandleTargetLocked;

        UnhookParryEvents();
    }

    private void HookParryEvents()
    {
        if (_eventsHooked) return;
        if (_ctx == null || _ctx.ParryController == null)
        {
            Invoke(nameof(HookParryEvents), 0.1f);
            return;
        }

        _ctx.ParryController.OnParryWindowOpened += HandleParryWindowOpened;
        _ctx.ParryController.OnParrySuccess += HandleParrySuccess;
        _ctx.ParryController.OnParryFail += HandleParryFail;

        _eventsHooked = true;
    }

    private void UnhookParryEvents()
    {
        if (!_eventsHooked) return;
        if (_ctx == null || _ctx.ParryController == null) return;

        _ctx.ParryController.OnParryWindowOpened -= HandleParryWindowOpened;
        _ctx.ParryController.OnParrySuccess -= HandleParrySuccess;
        _ctx.ParryController.OnParryFail -= HandleParryFail;

        _eventsHooked = false;
    }

    public Vector3 GetVelocityModifier() => CurrentStepVelocity;

    private void Awake()
    {
        _ctx = GetComponent<PlayerController>();
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
        _comboEngine = new PlayerComboEngine(_ctx, anim);
    }

    // =========================================================
    // UPDATE — chỉ update combo engine khi ĐANG trong PlayerAttackState
    // =========================================================
    private void Update()
    {
        UpdateComboTimers(stateNormal);
        UpdateComboTimers(stateE);
        UpdateComboTimers(stateQ);
        UpdateParryAnimation();
        HandleCamera();

        // Combo engine update khi đang attack
        if (IsAttacking && _ctx.CurrentState is PlayerAttackState)
        {
            _comboEngine.UpdateWindows();

            if (_comboEngine.IsAnimationFinished())
            {
                FinishComboAttack(_activeState);
            }
        }

        // 🔥 INPUT — chia 2 nhánh theo category
        var category = _ctx.CurrentState?.Category ?? StateCategory.Locomotion;

        if (category == StateCategory.Locomotion)
        {
            // Nhận input bình thường — start combo mới
            ProcessLocomotionInput();
        }
        else if (category == StateCategory.Combat && _ctx.CurrentState is PlayerAttackState)
        {
            // Đang attack — xử lý combo window / cancel
            HandleComboInput();
        }
        // Override → không nhận input
    }

    private void ProcessLocomotionInput()
    {
        if (playerInputs == null) return;

        // PARRY
        if (playerInputs.HasCommand(BufferedAction.Parry))
        {
            playerInputs.ConsumeCommand(BufferedAction.Parry);
            _ctx.RequestState(StateRequestType.Parry);
            _ctx.ParryController?.OnParryInput();
            OnAttackRequested?.Invoke();
            return;
        }

        // ATTACK
        if (!IsAttacking)
        {
            if (playerInputs.HasCommand(BufferedAction.NormalAttack))
            {
                _pendingComboType = ComboType.Normal;
                _ctx.RequestState(StateRequestType.Attack);
                OnAttackRequested?.Invoke();
            }
            else if (playerInputs.HasCommand(BufferedAction.ElementalSkill))
            {
                _pendingComboType = ComboType.SkillE;
                _ctx.RequestState(StateRequestType.Attack);
                OnAttackRequested?.Invoke();
            }
            else if (playerInputs.HasCommand(BufferedAction.ElementalBurst))
            {
                _pendingComboType = ComboType.BurstQ;
                _ctx.RequestState(StateRequestType.Attack);
                OnAttackRequested?.Invoke();
            }
        }
    }

    private void FinishComboAttack(ComboState state)
    {
        if (state == null) return;

        Debug.Log($"[PlayerSkillManager] FinishComboAttack — {state.associatedAction}");

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

    private void UpdateParryAnimation()
    {
        if (!_isPlayingParryAnimation) return;

        _parryAnimationTimer += Time.deltaTime;

        if (_parryAnimationTimer >= _parryAnimationDuration)
        {
            _isPlayingParryAnimation = false;
            IsParryActive = false;
        }
    }

    // =========================================================
    // PARRY EVENT HANDLERS
    // =========================================================
    private void HandleParryWindowOpened(int frames)
    {
        IsParryActive = true;
    }

    public string GetParryAnimationName()
    {
        if (parryCombo == null || parryCombo.attacks == null || parryCombo.attacks.Count == 0)
            return null;
        return parryCombo.attacks[0]?.animationName;
    }

    private void HandleParrySuccess(GameObject attacker) { }

    private void HandleParryFail()
    {
        IsParryActive = false;
        _isPlayingParryAnimation = false;
        _parryAnimationTimer = 0f;
    }

    // =========================================================
    // 🔥 PUBLIC API — GỌI TỪ PlayerAttackState
    // =========================================================

    /// <summary>
    /// Gọi từ PlayerAttackState.EnterState() — start combo thật sự.
    /// </summary>
    public void StartPendingCombo()
    {
        ComboState target = _pendingComboType switch
        {
            ComboType.Normal => stateNormal,
            ComboType.SkillE => stateE,
            ComboType.BurstQ => stateQ,
            _ => stateNormal
        };

        StartComboChain(target);
    }

    /// <summary>
    /// Gọi từ PlayerAttackState.ExitState() — dọn combo.
    /// </summary>
    public void OnAttackStateExit()
    {
        ForceCancelCombo(false);
        _pendingComboType = ComboType.Normal;
    }

    // =========================================================
    // INPUT → REQUEST
    // =========================================================
    private void ProcessInputAsRequests()
    {
        if (playerInputs == null) return;

        // Lấy category của state hiện tại
        var category = _ctx.CurrentState?.Category ?? StateCategory.Locomotion;

        // Combat và Override không nhận input mới (trừ trong state riêng)
        if (category == StateCategory.Combat) return;
        if (category == StateCategory.Override) return;

        // Locomotion → nhận mọi input
        // ═══ PARRY — ưu tiên cao nhất ═══
        if (playerInputs.HasCommand(BufferedAction.Parry))
        {
            playerInputs.ConsumeCommand(BufferedAction.Parry);
            _ctx.RequestState(StateRequestType.Parry);
            _ctx.ParryController?.OnParryInput();
            OnAttackRequested?.Invoke();
            return;
        }

        // ═══ ATTACK ═══
        if (!IsAttacking)
        {
            if (playerInputs.HasCommand(BufferedAction.NormalAttack))
            {
                _pendingComboType = ComboType.Normal;
                _ctx.RequestState(StateRequestType.Attack);
                OnAttackRequested?.Invoke();
            }
            else if (playerInputs.HasCommand(BufferedAction.ElementalSkill))
            {
                _pendingComboType = ComboType.SkillE;
                _ctx.RequestState(StateRequestType.Attack);
                OnAttackRequested?.Invoke();
            }
            else if (playerInputs.HasCommand(BufferedAction.ElementalBurst))
            {
                _pendingComboType = ComboType.BurstQ;
                _ctx.RequestState(StateRequestType.Attack);
                OnAttackRequested?.Invoke();
            }
        }
    }

    /// <summary>
    /// Xử lý input khi đã trong Attack state (combo window, cancel).
    /// </summary>
    private void HandleComboInput()
    {
        if (_comboEngine == null) return;
        if (!(_ctx.CurrentState is PlayerAttackState)) return;

        // Dash cancel
        if (_comboEngine.CanDashCancelNow && playerInputs.HasCommand(BufferedAction.Dash))
        {
            playerInputs.ConsumeCommand(BufferedAction.Dash);
            ForceCancelCombo(false);
            _ctx.RequestState(StateRequestType.Dash);
            OnDashCancelRequested?.Invoke();
            return;
        }

        // Jump cancel
        if (_comboEngine.CanJumpCancelNow && playerInputs.HasCommand(BufferedAction.Jump))
        {
            playerInputs.ConsumeCommand(BufferedAction.Jump);
            ForceCancelCombo(false);
            _ctx.RequestState(StateRequestType.Jump);
            OnJumpCancelRequested?.Invoke();
            return;
        }

        // Combo window — chuyển bước hoặc đổi combo
        if (_comboEngine.IsComboWindowActive)
        {
            if (playerInputs.HasCommand(BufferedAction.ElementalBurst))
            {
                _pendingComboType = ComboType.BurstQ;
                StartComboChain(stateQ);
                return;
            }

            if (playerInputs.HasCommand(BufferedAction.ElementalSkill))
            {
                _pendingComboType = ComboType.SkillE;
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
                    _pendingComboType = ComboType.Normal;
                    StartComboChain(stateNormal);
                }
                return;
            }
        }
    }

    // =========================================================
    // COMBO CHAIN MANAGEMENT
    // =========================================================
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

        _comboEngine.ChangeAttackData(state.sequence.attacks[state.currentIndex], CurrentRuntimeAttackType);
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

    // =========================================================
    // FORCE CANCEL — chỉ dọn dẹp dữ liệu, KHÔNG đụng state
    // =========================================================
    public void ForceCancelCombo(bool playIdleAnimation = true)
    {
        if (_activeState == null && !IsAttacking) return;

        _isForceCancelled = true;
        _activeState = null;

        foreach (var state in _allStates)
        {
            state.isAttacking = false;
            state.currentIndex = 0;
            state.comboResetTimer = 0f;
        }

        if (playerInputs != null)
        {
            playerInputs.ConsumeCommand(BufferedAction.NormalAttack);
            playerInputs.ConsumeCommand(BufferedAction.ElementalSkill);
            playerInputs.ConsumeCommand(BufferedAction.ElementalBurst);
        }

        _comboEngine?.ChangeAttackData(null, AttackType.NormalAttack);
    }

    private void HandleTargetLocked(GameObject target)
    {
        if (target == null) return;
        Vector3 direction = (target.transform.position - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero) transform.rotation = Quaternion.LookRotation(direction);
    }
}