using System;
using System.Collections.Generic;
using UnityEngine;
using Combat.Parry;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(CharacterController))]
public class PlayerController : CharacterActor,
                                IPlayerCombatEvents,
                                IParryCapable,
                                IParryReceiver
{
    #region CONFIGURATION

    [Header("GDC 2016 Constants")]
    [field: SerializeField] public float JumpHeight { get; private set; } = 6f;
    [field: SerializeField] public float TimeToJumpApex { get; private set; } = 0.4f;
    [field: SerializeField] public float RunMaxSpeed { get; private set; } = 12f;
    [field: SerializeField] public float RunAcceleration { get; private set; } = 90f;
    [field: SerializeField] public float Friction { get; private set; } = 30f;

    [Header("Variable Gravity")]
    [field: SerializeField] public float GravityScaling { get; private set; } = 2.5f;
    [field: SerializeField] public float FallClamp { get; private set; } = -30f;

    [Header("Coyote & Jump Buffer")]
    [field: SerializeField] public float CoyoteTime { get; private set; } = 0.15f;
    [field: SerializeField] public float JumpBufferTime { get; private set; } = 0.1f;

    [Header("Dash Settings")]
    [field: SerializeField] public float DashForce { get; private set; } = 35f;
    [field: SerializeField] public float DashCooldown { get; private set; } = 1f;
    [field: SerializeField] public float DashLength { get; private set; } = 7f;
    [field: SerializeField] public float DashDuration { get; private set; } = 0.25f;

    [field: SerializeField]
    public AnimationCurve DashCurve { get; private set; } = AnimationCurve.Linear(0, 1, 1, 0);

    [Header("Movement & Rotation")]
    [field: SerializeField] public float RotationSpeed { get; private set; } = 15f;
    [field: SerializeField] public float AirControl { get; private set; } = 5f;
    [field: SerializeField] public Transform MainCamera { get; private set; }

    [Header("Responsive Movement")]
    [field: SerializeField] public float TAttack { get; private set; } = 0.1f;
    [field: SerializeField] public float TRelease { get; private set; } = 0.15f;

    [Header("References")]
    [SerializeField] private PlayerSkillManager skillManager;
    public SO_PlayerConfiguration PlayerConfig;

    [Header("Hit Reaction")]
    [field: SerializeField] public float DashCancelStartTime { get; private set; } = 0.15f;

    #endregion

    #region COMPONENTS

    private CharacterController _charController;

    #endregion

    #region SERVICES

    private IPhysicsHandler _physicsHandler;
    private IRotationHandler _rotationHandler;
    private IAnimationHandler _animationHandler;
    private IInputHandler _inputHandler;
    private IMovementHandler _groundMovementHandler;
    private IMovementHandler _airMovementHandler;

    #endregion

    #region STATE MACHINE

    private PlayerStateFactory _states;
    private PlayerBaseState _currentState;

    private StateRequestType _pendingRequest = StateRequestType.None;
    private GameObject _pendingRequestSource;

    #endregion

    #region RUNTIME STATE

    private Vector3 _velocity;
    private float _jumpVelocity;
    private Vector2 _inputVector;
    private Vector3 _appliedMovement;

    private float _gravity;
    private float _initialJumpVelocity;

    private float _coyoteCounter;
    private float _jumpBufferCounter;
    private float _dashCooldownTimer;

    private int _groundedFrameCount;
    private int _airborneFrameCount;
    private const int GROUNDED_STABLE_FRAMES = 3;

    private bool _isDashing;
    private bool _isAttacking;
    private bool _rotationLocked;

    private float _hitReactionIFrameTimer;

    private Combat.Parry.ParryMovementType _pendingParryMovement;

    // Temp rotation lock (parry snap)
    private float _tempRotationLockTimer = 0f;
    private Quaternion _lockedRotation;

    #endregion

    #region INPUT

    public PlayerInputs _playerInputs;

    #endregion

    #region VELOCITY PROVIDERS

    private RootMotionVelocityProvider _rootMotionProvider;
    private readonly List<IVelocityProvider> _velocityProviders = new List<IVelocityProvider>();

    public void RegisterVelocityProvider(IVelocityProvider provider)
    {
        if (provider == null) return;
        if (!_velocityProviders.Contains(provider))
            _velocityProviders.Add(provider);
    }

    public void UnregisterVelocityProvider(IVelocityProvider provider)
    {
        if (provider == null) return;
        _velocityProviders.Remove(provider);
    }

    #endregion

    #region PROPERTIES

    public CharacterController CharController => _charController;

    public Vector3 Velocity
    {
        get => new Vector3(_velocity.x, _jumpVelocity, _velocity.z);
        set
        {
            _velocity.x = value.x;
            _velocity.z = value.z;
            _jumpVelocity = value.y;
        }
    }

    public float JumpVelocity
    {
        get => _jumpVelocity;
        set => _jumpVelocity = value;
    }

    public Vector2 InputVector => _inputVector;

    public Vector3 AppliedMovement
    {
        get => _appliedMovement;
        set => _appliedMovement = value;
    }

    public float Gravity => _gravity;
    public float InitialJumpVelocity => _initialJumpVelocity;

    public IPhysicsHandler PhysicsHandler => _physicsHandler;
    public IRotationHandler RotationHandler => _rotationHandler;
    public IAnimationHandler AnimationHandler => _animationHandler;
    public IInputHandler InputHandler => _inputHandler;
    public IMovementHandler GroundMovementHandler => _groundMovementHandler;
    public IMovementHandler AirMovementHandler => _airMovementHandler;

    public float CoyoteCounter
    {
        get => _coyoteCounter;
        set => _coyoteCounter = Mathf.Max(0f, value);
    }

    public float JumpBufferCounter
    {
        get => _jumpBufferCounter;
        set => _jumpBufferCounter = Mathf.Max(0f, value);
    }

    public PlayerStateFactory States { get => _states; set => _states = value; }
    public PlayerBaseState CurrentState { get => _currentState; set => _currentState = value; }
    public bool IsAttacking => _isAttacking;

    public bool IsGroundedRaw => _charController.isGrounded;
    public bool IsGroundedStable => _groundedFrameCount >= GROUNDED_STABLE_FRAMES;
    public bool IsAirborneStable => _airborneFrameCount >= GROUNDED_STABLE_FRAMES;

    public float HitReactionIFrameTimer => _hitReactionIFrameTimer;

    public bool IsInParryMovement => CurrentState is PlayerParryMovementState;

    #endregion

    #region ANIMATION HASHES

    public readonly int ID_Idle = Animator.StringToHash("HumanM@Idle01");
    public readonly int Anim_Run_F = Animator.StringToHash("HumanM@Run01_Forward");
    public readonly int Anim_Run_B = Animator.StringToHash("HumanM@Run01_Backward");
    public readonly int Anim_Run_L = Animator.StringToHash("HumanM@Run01_Left");
    public readonly int Anim_Run_R = Animator.StringToHash("HumanM@Run01_Right");
    public readonly int Anim_Run_FL = Animator.StringToHash("HumanM@Run01_ForwardLeft");
    public readonly int Anim_Run_FR = Animator.StringToHash("HumanM@Run01_ForwardRight");
    public readonly int Anim_Run_BL = Animator.StringToHash("HumanM@Run01_BackwardLeft");
    public readonly int Anim_Run_BR = Animator.StringToHash("HumanM@Run01_BackwardRight");

    public readonly int Anim_Jump_Begin = Animator.StringToHash("HumanM@Jump01 - Begin");
    public readonly int Anim_Falling = Animator.StringToHash("HumanM@Fall01");
    public readonly int Anim_Land = Animator.StringToHash("HumanM@Jump01 - Land");
    public readonly int Anim_Dash = Animator.StringToHash("HumanM@Dash01");
    public readonly int Anim_DashBack = Animator.StringToHash("DashBack");
    public readonly int HitReactionAnimName = Animator.StringToHash("KnockBack");
    public readonly int Anim_Parry = Animator.StringToHash("HumanM@Parry01");

    // Animator layers
    public const int Layer_Base = 0;
    public const int Layer_Attack = 0;
    public const int Layer_Parry = 2;

    private readonly Dictionary<Combat.Parry.ParryMovementType, int> _parryMovementHashes
        = new Dictionary<Combat.Parry.ParryMovementType, int>
    {
        { Combat.Parry.ParryMovementType.Forward,       Animator.StringToHash("HumanM@Parry_Jump_F") },
        { Combat.Parry.ParryMovementType.Backward,      Animator.StringToHash("HumanM@Parry_Backflip") },
        { Combat.Parry.ParryMovementType.Left,          Animator.StringToHash("HumanM@Parry_Jump_L") },
        { Combat.Parry.ParryMovementType.Right,         Animator.StringToHash("HumanM@Parry_Jump_R") },
        { Combat.Parry.ParryMovementType.ForwardLeft,   Animator.StringToHash("HumanM@Parry_Jump_FL") },
        { Combat.Parry.ParryMovementType.ForwardRight,  Animator.StringToHash("HumanM@Parry_Jump_FR") },
        { Combat.Parry.ParryMovementType.BackwardLeft,  Animator.StringToHash("HumanM@Parry_Jump_BL") },
        { Combat.Parry.ParryMovementType.BackwardRight, Animator.StringToHash("HumanM@Parry_Jump_BR") },
    };

    public int GetParryMovementHash(Combat.Parry.ParryMovementType type)
    {
        if (_parryMovementHashes.TryGetValue(type, out int h))
            return h;

        Debug.LogWarning($"[PlayerController] ParryMovement '{type}' không có hash!");
        return -1;
    }

    #endregion

    #region DETECTION

    [SerializeField] private CombatDetection combatDetection;
    public CombatDetection CombatDetection => combatDetection;

    #endregion

    #region EVENTS

    public event Action<AttackType> OnPlayerSkillCast;
    public void RaiseSkillCast(AttackType skillType) => OnPlayerSkillCast?.Invoke(skillType);
    public int GetGroundedFrameCount() => _groundedFrameCount;
    public int GetAirborneFrameCount() => _airborneFrameCount;

    protected override void OnEnable()
    {
        base.OnEnable();
        if (skillManager != null)
        {
            skillManager.OnAttackRequested += HandleAttackRequest;
            skillManager.OnDashCancelRequested += HandleDashCancelRequest;
            skillManager.OnJumpCancelRequested += HandleJumpCancelRequest;
        }
    }

    protected override void OnDisable()
    {
        if (skillManager != null)
        {
            skillManager.OnAttackRequested -= HandleAttackRequest;
            skillManager.OnDashCancelRequested -= HandleDashCancelRequest;
            skillManager.OnJumpCancelRequested -= HandleJumpCancelRequest;
        }
        base.OnDisable();
    }

    private void HandleAttackRequest() => RequestState(StateRequestType.Attack);
    private void HandleDashCancelRequest() => RequestState(StateRequestType.Dash);
    private void HandleJumpCancelRequest() => RequestState(StateRequestType.Jump);

    #endregion

    #region UNITY LIFECYCLE

    private void OnAnimatorMove()
    {
        if (Animator == null) return;
        if (!ShouldApplyRootMotion()) return;

        var attackData = skillManager?.CurrentAttackData;
        if (attackData == null) return;

        Vector3 worldDelta = Animator.deltaPosition * attackData.rootMotionMultiplier;
        worldDelta.y = 0f;

        _rootMotionProvider?.FeedDelta(worldDelta);
    }

    protected override void Awake()
    {
        base.Awake();

        InitializeComponents();
        InitializeReferences();
        ComputePhysicsConstants();
        InitializeServices();

        States = new PlayerStateFactory(this);
    }

    private void Start()
    {
        _rotationLocked = false;
        _isAttacking = false;
        _isDashing = false;

        CurrentState = States.Grounded();
        CurrentState.EnterState();

        Debug.Log($"[Start] rotationLocked={_rotationLocked}, attacking={_isAttacking}");
    }

    private bool ShouldApplyRootMotion()
    {
        if (CurrentState == null) return false;

        if (CurrentState is PlayerAttackState)
        {
            var attackData = skillManager?.CurrentAttackData;
            if (attackData == null) return false;
            return attackData.useRootMotion;
        }

        return false;
    }

    private void Update()
    {
        if (CurrentState == null) return;

        ReadInput();
        UpdateTimers();
        UpdateFrameCounters();

        CurrentState.UpdateStates();

        UpdateTempRotationLock();

        ProcessStateRequests();

        ApplyGravity();
        ApplyMovement();
    }

    #endregion

    #region ABSTRACT IMPLEMENTATIONS (CharacterActor)

    public override bool IsPlayer => true;

    public override Vector3 GetLookDirection()
    {
        if (_inputHandler == null)
            return Model != null ? Model.forward : transform.forward;

        Vector3 dir = _inputHandler.GetMovementDirection(_inputVector);
        if (dir == Vector3.zero)
            return Model != null ? Model.forward : transform.forward;
        return dir;
    }

    public override Transform GetAttackTarget() => null;

    protected override ComboEngineBase CreateComboEngine()
        => new PlayerComboEngine(this, Animator);

    protected override float GetDamageMultiplier(AttackType type) => 100f;

    protected override bool CanTakeDamage()
    {
        if (CurrentState is PlayerDashState) return false;
        if (CurrentState is PlayerParryMovementState) return false;
        if (CurrentState is PlayerAirDashState) return false;
        if (CurrentState is PlayerParryState) return false;
        return true;
    }

    public override void TakeDMG(int damage, bool isCRIT)
    {
        if (!CanTakeDamage()) return;

        base.TakeDMG(damage, isCRIT);
        DMGPopUpGenerator.Instance?.Create(transform.position + Vector3.up * 1.5f, damage, isCRIT, false);

        RequestState(StateRequestType.Hit);
        _hitReactionIFrameTimer = HitReactionIFrameDuration;

        ScreenShakeManager.Instance?.TriggerShake(isCRIT ? 0.5f : 0.3f);
        HitStopSystem.Instance?.Trigger(0.05f, 0.1f);
    }

    #endregion

    #region PARRY MOVEMENT API

    public void RequestParryMovement(Combat.Parry.ParryMovementType type)
    {
        _pendingParryMovement = type;

        _pendingRequest = StateRequestType.ParryMovement;
        _pendingRequestSource = null;
    }

    public Combat.Parry.ParryMovementType ConsumePendingParryMovement()
    {
        var m = _pendingParryMovement;
        _pendingParryMovement = Combat.Parry.ParryMovementType.None;
        return m;
    }

    #endregion

    #region STATE REQUEST API

    public void RequestState(StateRequestType type, GameObject source = null)
    {
        if ((int)type >= (int)_pendingRequest)
        {
            _pendingRequest = type;
            _pendingRequestSource = source;
        }
    }

    private void ProcessStateRequests()
    {
        if (_pendingRequest != StateRequestType.None)
        {
            var request = _pendingRequest;
            var source = _pendingRequestSource;
            _pendingRequest = StateRequestType.None;
            _pendingRequestSource = null;

            switch (request)
            {
                case StateRequestType.Dead:
                    if (!(CurrentState is PlayerDeadState))
                        ForceSwitchState(States.Dead());
                    return;

                case StateRequestType.Hit:
                    HandleHitRequest(source);
                    return;

                case StateRequestType.Parry:
                    HandleParryRequest();
                    return;

                case StateRequestType.Attack:
                    HandleAttackStateRequest();
                    return;

                case StateRequestType.Dash:
                    HandleDashStateRequest();
                    return;

                case StateRequestType.Jump:
                    HandleJumpStateRequest();
                    return;

                case StateRequestType.ParryMovement:
                    HandleParryMovementRequest();
                    return;
            }
        }

        HandleImplicitTransitions();
    }

    private void HandleImplicitTransitions()
    {
        if (CurrentState is IImplicitTransitionable transitionable && transitionable.IsFinished)
        {
            SwitchState(ResolveGroundState());
            return;
        }
    }

    private void HandleHitRequest(GameObject attacker)
    {
        var category = CurrentState?.Category ?? StateCategory.Locomotion;
        if (category == StateCategory.Override) return;

        if (CurrentState is PlayerDashState) return;
        if (CurrentState is PlayerParryMovementState) return;
        if (CurrentState is PlayerAirDashState) return;

        // 🔥 Chỉ block khi parry window CÒN MỞ (đang thực sự parry)
        if (CurrentState is PlayerParryState parryState)
        {
            var pc = ParryController;
            bool windowStillOpen = pc != null
                && pc.CurrentState == Combat.Parry.ParryState.WindowOpen
                && pc.CurrentWindowFrames > 0;

            if (windowStillOpen) return;   // đang parry → không cho hit chen
                                           // ngược lại: parry đã hụt → cho hit vào
        }

        ForceSwitchState(States.Hit());
    }
    private void HandleParryRequest()
    {
        if (CurrentState is PlayerParryState) return;

        skillManager?.ForceCancelCombo(false);

        Vector3 v = Velocity;
        SetVelocity(v.x * 0.3f, v.y, v.z * 0.3f);

        ForceSwitchState(States.Parry());
    }

    private void HandleAttackStateRequest()
    {
        var category = CurrentState?.Category ?? StateCategory.Locomotion;

        if (category == StateCategory.Combat) return;
        if (category == StateCategory.Override) return;

        SwitchState(States.Attack());
    }

    private void HandleDashStateRequest()
    {
        if (!TryDash) return;

        var category = CurrentState?.Category ?? StateCategory.Locomotion;
        if (category == StateCategory.Override) return;

        if (CurrentState is PlayerGroundedState groundedState)
        {
            groundedState.SetChildState(States.Dash());
            return;
        }

        if (CurrentState is PlayerAirborneState airborneState)
        {
            airborneState.SetChildState(States.AirDash());
            return;
        }

        if (category == StateCategory.Combat)
        {
            ForceSwitchState(States.Grounded());
            if (CurrentState is PlayerGroundedState gState)
            {
                gState.SetChildState(States.Dash());
            }
            return;
        }

        Debug.LogWarning($"[PlayerController] HandleDashStateRequest fallback từ {CurrentState?.GetType().Name}");
        SwitchState(States.Grounded());
    }

    private void HandleJumpStateRequest()
    {
        if (!TryJump) return;

        var category = CurrentState?.Category ?? StateCategory.Locomotion;
        if (category == StateCategory.Override) return;
        if (category == StateCategory.Combat) return;

        SwitchState(States.Airborne());
    }

    private void HandleParryMovementRequest()
    {
        if (CurrentState is PlayerDeadState) return;
        if (CurrentState is PlayerHitState) return;

        skillManager?.ForceCancelCombo(false);
        _playerInputs?.ClearBuffer();

        ForceSwitchState(States.ParryMovement());
    }

    private void SwitchState(PlayerBaseState newState)
    {
        if (newState == null) return;
        if (CurrentState != null && CurrentState.GetType() == newState.GetType()) return;

        CurrentState?.ForceExit();
        CurrentState = newState;

        _rotationLocked = false;

        CurrentState.EnterState();
    }

    private void ForceSwitchState(PlayerBaseState newState)
    {
        if (newState == null) return;

        CurrentState?.ForceExit();
        CurrentState = newState;

        _rotationLocked = false;

        CurrentState.EnterState();
    }

    #endregion

    #region INITIALIZATION

    private void InitializeComponents()
    {
        _charController = GetComponent<CharacterController>();

        if (skillManager == null)
            skillManager = GetComponent<PlayerSkillManager>();
    }

    private void InitializeReferences()
    {
        if (MainCamera == null && Camera.main != null)
            MainCamera = Camera.main.transform;

        if (Animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    public void SwitchStateExternal(PlayerBaseState newState)
    {
        SwitchState(newState);
    }

    private void InitializeServices()
    {
        _rootMotionProvider = new RootMotionVelocityProvider();
        RegisterVelocityProvider(_rootMotionProvider);

        _physicsHandler = new GravityHandler(_gravity, GravityScaling, FallClamp);
        _rotationHandler = new ModelRotationHandler();

        _animationHandler = new MovementAnimationHandler(
            Animator, ID_Idle,
            Anim_Run_F, Anim_Run_B, Anim_Run_L, Anim_Run_R,
            Anim_Run_FL, Anim_Run_FR, Anim_Run_BL, Anim_Run_BR);

        _inputHandler = new CameraRelativeInputHandler(MainCamera, _playerInputs);

        _groundMovementHandler = new ResponsiveMovementHandler(RunMaxSpeed, TAttack, TRelease);
        _airMovementHandler = new ResponsiveMovementHandler(RunMaxSpeed, TAttack * 2f, TRelease * 2f);
    }

    private void ComputePhysicsConstants()
    {
        _gravity = -(2f * JumpHeight) / Mathf.Pow(TimeToJumpApex, 2f);
        _initialJumpVelocity = Mathf.Abs(_gravity) * TimeToJumpApex;
    }

    #endregion

    #region UPDATE FLOW

    private void ReadInput()
    {
        if (_inputHandler != null)
            _inputVector = _inputHandler.ReadMovementInput();
    }

    private void UpdateTimers()
    {
        if (_playerInputs != null && _playerInputs.JumpHeld)
            _jumpBufferCounter = JumpBufferTime;
        else
            _jumpBufferCounter = Mathf.Max(0f, _jumpBufferCounter - Time.deltaTime);

        if (_charController.isGrounded)
            _coyoteCounter = CoyoteTime;
        else
            _coyoteCounter = Mathf.Max(0f, _coyoteCounter - Time.deltaTime);

        if (_dashCooldownTimer > 0f)
            _dashCooldownTimer -= Time.deltaTime;

        if (_hitReactionIFrameTimer > 0f)
            _hitReactionIFrameTimer -= Time.deltaTime;
    }

    private void UpdateFrameCounters()
    {
        if (_charController.isGrounded)
        {
            _groundedFrameCount++;
            _airborneFrameCount = 0;
        }
        else
        {
            _airborneFrameCount++;
            _groundedFrameCount = 0;
        }
    }

    #endregion

    #region GROUND STATE RESOLUTION

    public PlayerBaseState ResolveGroundState()
    {
        if (_charController.isGrounded)
        {
            if (_jumpVelocity < 3f)
                return States.Grounded();
            if (_jumpVelocity < 5f && IsGroundedStable)
                return States.Grounded();
        }

        if (IsGroundedStable)
            return States.Grounded();

        if (_groundedFrameCount > 0 && _jumpVelocity < 1f)
            return States.Grounded();

        return States.Airborne();
    }

    #endregion

    #region MOVEMENT

    private void ApplyGravity()
    {
        if (HasExclusiveProviderActive())
        {
            _jumpVelocity = 0f;
            return;
        }

        if (_charController.isGrounded && _jumpVelocity < 0f && !_isDashing)
        {
            _jumpVelocity = -2f;
        }
        else
        {
            _jumpVelocity += _gravity * GravityScaling * Time.deltaTime;
            _jumpVelocity = Mathf.Max(_jumpVelocity, FallClamp);
        }
    }

    private void ApplyMovement()
    {
        // 1. Exclusive provider
        for (int i = 0; i < _velocityProviders.Count; i++)
        {
            var p = _velocityProviders[i];
            if (p == null || !p.IsActive) continue;

            if (p is IExclusiveVelocityProvider)
            {
                _charController.Move(p.GetVelocityModifier() * Time.deltaTime);
                return;
            }
        }

        // 2. Non-exclusive — Priority cao nhất
        IVelocityProvider best = null;
        int bestPriority = int.MinValue;
        for (int i = 0; i < _velocityProviders.Count; i++)
        {
            var p = _velocityProviders[i];
            if (p == null || !p.IsActive) continue;
            if (p.Priority > bestPriority) { best = p; bestPriority = p.Priority; }
        }

        if (best != null)
        {
            _charController.Move(best.GetVelocityModifier() * Time.deltaTime);
            return;
        }

        // 3. Fallback
        Vector3 motion = new Vector3(_velocity.x, _jumpVelocity, _velocity.z);
        _charController.Move(motion * Time.deltaTime);
    }

    private bool HasExclusiveProviderActive()
    {
        for (int i = 0; i < _velocityProviders.Count; i++)
        {
            var p = _velocityProviders[i];
            if (p != null && p.IsActive && p is IExclusiveVelocityProvider)
                return true;
        }
        return false;
    }

    #endregion

    #region ROTATION

    public void RotateModel(Vector3 direction)
    {
        if (Model == null || _rotationLocked) return;
        if (direction.sqrMagnitude < 0.001f) return;

        Model.rotation = Quaternion.LookRotation(direction.normalized);
    }

    public void RotateTowardDirection(Transform model, Vector3 direction, float rotationSpeed)
    {
        if (model == null) return;
        if (direction.sqrMagnitude < 0.01f) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        float t = rotationSpeed * Time.deltaTime;
        model.rotation = Quaternion.Slerp(model.rotation, targetRotation, t);
    }

    public void HandleRotation()
    {
        if (_rotationLocked) return;
        if (_inputVector.sqrMagnitude <= 0.01f) return;

        Vector3 moveDirection = GetLookDirection();
        _rotationHandler?.RotateTowardDirection(Model, moveDirection, RotationSpeed);
    }

    public void LockRotationForDuration(float duration)
    {
        if (duration <= 0f) return;
        _tempRotationLockTimer = duration;
        _rotationLocked = true;
        if (Model != null) _lockedRotation = Model.rotation;
    }

    private void UpdateTempRotationLock()
    {
        if (_tempRotationLockTimer <= 0f) return;

        _tempRotationLockTimer -= Time.deltaTime;

        if (Model != null)
            Model.rotation = _lockedRotation;

        if (_tempRotationLockTimer <= 0f)
            _rotationLocked = false;
    }

    #endregion

    #region INPUT ACTIONS

    public bool TryNormalAttack =>
        !IsAttacking &&
        _playerInputs != null &&
        _playerInputs.HasCommand(BufferedAction.NormalAttack);

    public bool TryDash =>
        _dashCooldownTimer <= 0f &&
        _playerInputs != null &&
        _playerInputs.HasCommand(BufferedAction.Dash);

    public bool TryJump =>
        _playerInputs != null &&
        _playerInputs.HasCommand(BufferedAction.Jump);

    #endregion

    #region ANIMATION

    public int GetMovementAnimation()
    {
        return _animationHandler != null
            ? _animationHandler.GetMovementAnimation(_inputVector, IsAttacking)
            : 0;
    }

    public void PlayAnimation(int animHash, float transition = 0.1f)
        => _animationHandler?.PlayAnimation(animHash, transition);

    #endregion

    #region COMBAT LOCKS

    public void SetAttackLock(bool value) => _isAttacking = value;
    public void SetRotationLock(bool value) => _rotationLocked = value;
    public void ResetDashCooldown() => _dashCooldownTimer = DashCooldown;

    #endregion

    #region UTILITIES

    public Vector3 GetHorizontalDashDirection()
    {
        Vector3 forwardDir;

        if (_inputVector.sqrMagnitude > 0.01f && MainCamera != null)
        {
            Vector3 cameraForward = MainCamera.forward;
            Vector3 cameraRight = MainCamera.right;

            cameraForward.y = 0f;
            cameraRight.y = 0f;
            cameraForward.Normalize();
            cameraRight.Normalize();

            forwardDir = cameraForward * _inputVector.y + cameraRight * _inputVector.x;
        }
        else
        {
            forwardDir = MainCamera != null ? -MainCamera.forward : -transform.forward;
            forwardDir.y = 0f;
        }

        return forwardDir.normalized;
    }

    public void SetVelocity(float x, float y, float z)
    {
        _velocity.x = x;
        _velocity.z = z;
        _jumpVelocity = y;
    }

    #endregion

    #region GUI DEBUG

    private GUIStyle _cachedStyle;

    private void OnGUI()
    {
        if (CurrentState == null) return;

        if (_cachedStyle == null)
        {
            _cachedStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            _cachedStyle.normal.textColor = Color.yellow;
        }

        string stateText = GetFormattedStateName(CurrentState);
        string info = $"<b>State:</b> {stateText}\n" +
                      $"<b>ParryMove:</b> {IsInParryMovement} | " +
                      $"<b>PendingReq:</b> {_pendingRequest}";

        GUI.Box(new Rect(10, 10, 500, 60), info, _cachedStyle);
    }

    private string GetFormattedStateName(PlayerBaseState state)
    {
        if (state == null) return "None";
        string name = state.GetType().Name;
        if (state.ChildState != null)
            name += " → " + GetFormattedStateName(state.ChildState);
        return name;
    }

    #endregion
}