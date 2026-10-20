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
    [Tooltip("Index 0 = parry base, Index 1-8 = 8 hướng (nếu có). Mỗi AttackData cần gán animationName + clip.")]
    [SerializeField] private ComboSequence parryCombo;

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

    public event Action OnAttackRequested;
    public event Action OnDashCancelRequested;
    public event Action OnJumpCancelRequested;

    private bool _eventsHooked = false;

    // Combo input lock — chống double-consume khi loop về đòn đầu
    private const float COMBO_INPUT_LOCK_TIME = 0.05f;
    private float _comboInputLockTimer = 0f;

    // =========================================================
    // LIFECYCLE
    // =========================================================
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

    // =========================================================
    // UPDATE
    // =========================================================
    private void Update()
    {
        if (_comboInputLockTimer > 0f)
            _comboInputLockTimer -= Time.deltaTime;

        UpdateComboTimers(stateNormal);
        UpdateComboTimers(stateE);
        UpdateComboTimers(stateQ);
        HandleCamera();

        // PRIORITY 1: PARRY
        if (playerInputs != null && playerInputs.HasCommand(BufferedAction.Parry))
        {
            var cat = _ctx.CurrentState?.Category ?? StateCategory.Locomotion;

            if (_ctx.CurrentState is PlayerParryState parryState)
            {
                if (playerInputs.ConsumeCommand(BufferedAction.Parry))
                {
                    parryState.RefreshTimer();
                    _ctx.ParryController?.OnParryInput();
                }
                return;
            }

            if (cat != StateCategory.Override)
            {
                playerInputs.ConsumeCommand(BufferedAction.Parry);
                ForceCancelCombo(false);
                _ctx.RequestState(StateRequestType.Parry);
                return;
            }
        }

        // PRIORITY 2: ATTACK
        if (IsAttacking && _ctx.CurrentState is PlayerAttackState)
        {
            _comboEngine.UpdateWindows();
            if (_comboEngine.IsAnimationFinished())
            {
                FinishComboAttack(_activeState);
                return;
            }
            HandleComboInput();
            return;
        }

        // PRIORITY 3: LOCOMOTION
        var category = _ctx.CurrentState?.Category ?? StateCategory.Locomotion;
        if (category == StateCategory.Locomotion)
        {
            ProcessLocomotionInput();
        }
    }

    private void ProcessLocomotionInput()
    {
        if (playerInputs == null) return;

        if (!IsAttacking)
        {
            if (playerInputs.HasCommand(BufferedAction.NormalAttack))
            {
                playerInputs.ConsumeCommand(BufferedAction.NormalAttack);
                _pendingComboType = ComboType.Normal;
                _ctx.RequestState(StateRequestType.Attack);
                OnAttackRequested?.Invoke();
                return;
            }

            if (playerInputs.HasCommand(BufferedAction.ElementalSkill))
            {
                playerInputs.ConsumeCommand(BufferedAction.ElementalSkill);
                _pendingComboType = ComboType.SkillE;
                _ctx.RequestState(StateRequestType.Attack);
                OnAttackRequested?.Invoke();
                return;
            }

            if (playerInputs.HasCommand(BufferedAction.ElementalBurst))
            {
                playerInputs.ConsumeCommand(BufferedAction.ElementalBurst);
                _pendingComboType = ComboType.BurstQ;
                _ctx.RequestState(StateRequestType.Attack);
                OnAttackRequested?.Invoke();
                return;
            }
        }
    }

    private void FinishComboAttack(ComboState state)
    {
        if (state == null) return;

        state.isAttacking = false;
        state.comboResetTimer = 1.0f;
        state.currentIndex = 0;
        _activeState = null;
    }

    private void UpdateComboTimers(ComboState state)
    {
        if (state.sequence == null) return;
        if (state.comboResetTimer > 0 && !state.isAttacking)
        {
            state.comboResetTimer -= Time.unscaledDeltaTime;
            if (state.comboResetTimer <= 0)
            {
                state.currentIndex = 0;
            }
        }
    }

    // =========================================================
    // PARRY EVENT HANDLERS
    // =========================================================
    private void HandleParryWindowOpened(int frames) => IsParryActive = true;
    private void HandleParrySuccess(GameObject attacker) { }
    private void HandleParryFail() => IsParryActive = false;

    #region PARRY ANIMATION API

    // Cache clip theo tên state — tránh query Animator mỗi frame
    private Dictionary<string, float> _clipLengthCache;

    /// <summary>
    /// Lấy độ dài clip (giây) theo tên state. Query từ runtimeAnimatorController, cache lại.
    /// </summary>
    public float GetClipLengthByName(string stateName)
    {
        if (string.IsNullOrEmpty(stateName) || _ctx == null || _ctx.Animator == null)
            return 0f;

        if (_clipLengthCache == null)
            _clipLengthCache = new Dictionary<string, float>();

        if (_clipLengthCache.TryGetValue(stateName, out float cached))
            return cached;

        var controller = _ctx.Animator.runtimeAnimatorController;
        if (controller == null) return 0f;

        foreach (var clip in controller.animationClips)
        {
            if (clip == null) continue;
            // Match theo tên clip == tên state (thường trùng)
            if (clip.name == stateName)
            {
                _clipLengthCache[stateName] = clip.length;
                return clip.length;
            }
        }

        // Fallback: nếu không match tên, thử match bằng hash
        // (một số setup đặt tên clip khác tên state — hiếm)
        _clipLengthCache[stateName] = 0f;
        return 0f;
    }

    /// <summary>
    /// Lấy tên animation state parry theo hướng movement.
    /// parryCombo.attacks index 0 = parry base, 1-8 = 8 hướng.
    /// Fallback về index 0 nếu hướng đó chưa có.
    /// </summary>
    public string GetParryAnimationName(Combat.Parry.ParryMovementType movement)
    {
        if (parryCombo == null || parryCombo.attacks == null) return null;

        int index = MovementToIndex(movement);

        if (index < parryCombo.attacks.Count && parryCombo.attacks[index] != null)
            return parryCombo.attacks[index].animationName;

        return parryCombo.attacks.Count > 0 ? parryCombo.attacks[0]?.animationName : null;
    }

    /// <summary>
    /// Lấy độ dài clip parry theo hướng movement — query Animator Controller.
    /// </summary>
    public float GetParryClipLength(Combat.Parry.ParryMovementType movement)
    {
        string name = GetParryAnimationName(movement);
        return GetClipLengthByName(name);
    }

    private static int MovementToIndex(Combat.Parry.ParryMovementType movement)
    {
        return movement switch
        {
            Combat.Parry.ParryMovementType.None => 0,
            Combat.Parry.ParryMovementType.Forward => 1,
            Combat.Parry.ParryMovementType.Backward => 2,
            Combat.Parry.ParryMovementType.Left => 3,
            Combat.Parry.ParryMovementType.Right => 4,
            Combat.Parry.ParryMovementType.ForwardLeft => 5,
            Combat.Parry.ParryMovementType.ForwardRight => 6,
            Combat.Parry.ParryMovementType.BackwardLeft => 7,
            Combat.Parry.ParryMovementType.BackwardRight => 8,
            _ => 0
        };
    }

    #endregion
    // =========================================================
    // PUBLIC API
    // =========================================================
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

    public void OnAttackStateExit()
    {
        ForceCancelCombo(false);
        _pendingComboType = ComboType.Normal;
    }

    // =========================================================
    // INPUT → COMBO
    // =========================================================
    private void HandleComboInput()
    {
        if (_comboEngine == null) return;
        if (!(_ctx.CurrentState is PlayerAttackState)) return;
        if (_activeState == null) return;
        if (_comboInputLockTimer > 0f) return;

        // Parry — ưu tiên cao nhất
        if (playerInputs.HasCommand(BufferedAction.Parry))
        {
            playerInputs.ConsumeCommand(BufferedAction.Parry);
            ForceCancelCombo(false);
            _ctx.RequestState(StateRequestType.Parry);
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
                playerInputs.ConsumeCommand(BufferedAction.ElementalBurst);
                _pendingComboType = ComboType.BurstQ;
                StartComboChain(stateQ);
                return;
            }

            if (playerInputs.HasCommand(BufferedAction.ElementalSkill))
            {
                playerInputs.ConsumeCommand(BufferedAction.ElementalSkill);
                _pendingComboType = ComboType.SkillE;
                StartComboChain(stateE);
                return;
            }

            if (playerInputs.HasCommand(BufferedAction.NormalAttack))
            {
                playerInputs.ConsumeCommand(BufferedAction.NormalAttack);
                if (_activeState == stateNormal)
                {
                    MoveToNextComboStep(stateNormal);
                    _comboInputLockTimer = COMBO_INPUT_LOCK_TIME;
                }
                else
                {
                    _pendingComboType = ComboType.Normal;
                    stateNormal.currentIndex = 0;
                    StartComboChain(stateNormal);
                }
                return;
            }
        }
    }

    // =========================================================
    // COMBO CHAIN
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

        if (_activeState != state)
        {
            state.currentIndex = 0;
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
            SkillCameraZoom.Instance?.ZoomIn();
        else
            SkillCameraZoom.Instance?.ZoomOut();
    }

    private void MoveToNextComboStep(ComboState state)
    {
        int nextIndex = state.currentIndex + 1;

        if (nextIndex >= state.sequence.attacks.Count)
        {
            FinishComboAttack(state);
            return;
        }

        state.currentIndex = nextIndex;
        ExecuteComboStep(state);
    }

    // =========================================================
    // FORCE CANCEL
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