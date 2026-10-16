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

    private bool _isInParryMovement;
    private Vector3 _parryMovementVelocity;

    private Combat.Parry.ParryMovementType _pendingParryMovement;

    #endregion

    #region INPUT

    public PlayerInputs _playerInputs;

    #endregion

    #region VELOCITY PROVIDERS

    private readonly List<IVelocityProvider> _velocityProviders = new List<IVelocityProvider>();

    public void RegisterVelocityProvider(IVelocityProvider provider) => _velocityProviders.Add(provider);
    public void UnregisterVelocityProvider(IVelocityProvider provider) => _velocityProviders.Remove(provider);

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
    public bool IsInParryMovement => _isInParryMovement;

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

    protected override void Awake()
    {
        base.Awake(); // CharacterActor.Awake — init Health, Config, ParryController, ComboEngine

        InitializeComponents();
        InitializeReferences();
        ComputePhysicsConstants();
        InitializeServices();

        States = new PlayerStateFactory(this);
    }

    private void Start()
    {
        CurrentState = States.Grounded();
        CurrentState.EnterState();
    }

    private void Update()
    {
        if (CurrentState == null) return;

        ReadInput();
        UpdateTimers();
        UpdateFrameCounters();

        CurrentState.UpdateStates();

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

    public void BeginParryMovement(Vector3 velocity)
    {
        _isInParryMovement = true;
        _parryMovementVelocity = velocity;

        _velocity = Vector3.zero;
        _jumpVelocity = 0f;
        _appliedMovement = Vector3.zero;
    }

    public void RequestParryMovement(Combat.Parry.ParryMovementType type)
    {
        _pendingParryMovement = type;

        // FORCE — không qua priority check
        _pendingRequest = StateRequestType.ParryMovement;
        _pendingRequestSource = null;
    }

    public Combat.Parry.ParryMovementType ConsumePendingParryMovement()
    {
        var m = _pendingParryMovement;
        _pendingParryMovement = Combat.Parry.ParryMovementType.None;
        return m;
    }

    public void UpdateParryMovementVelocity(Vector3 velocity)
    {
        _parryMovementVelocity = velocity;
    }

    public void EndParryMovement()
    {
        _isInParryMovement = false;
        _parryMovementVelocity = Vector3.zero;
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
        HandleImplicitTransitions();

        if (_pendingRequest == StateRequestType.None) return;

        var request = _pendingRequest;
        var source = _pendingRequestSource;

        _pendingRequest = StateRequestType.None;
        _pendingRequestSource = null;

        switch (request)
        {
            case StateRequestType.Dead:
                if (!(CurrentState is PlayerDeadState))
                    ForceSwitchState(States.Dead());
                break;

            case StateRequestType.Hit:
                HandleHitRequest(source);
                break;

            case StateRequestType.Parry:
                HandleParryRequest();
                break;

            case StateRequestType.Attack:
                HandleAttackStateRequest();
                break;

            case StateRequestType.Dash:
                HandleDashStateRequest();
                break;

            case StateRequestType.Jump:
                HandleJumpStateRequest();
                break;

            case StateRequestType.ParryMovement:
                HandleParryMovementRequest();
                break;
        }
    }

    private void HandleImplicitTransitions()
    {
        if (CurrentState is PlayerAttackState && (skillManager == null || !skillManager.IsAttacking))
        {
            SwitchState(ResolveGroundState());
            return;
        }

        if (CurrentState is PlayerHitState hitState && hitState.IsFinished)
        {
            SwitchState(ResolveGroundState());
            return;
        }

        if (CurrentState is PlayerParryState parryState && parryState.IsFinished)
        {
            SwitchState(ResolveGroundState());
            return;
        }

        if (CurrentState is PlayerParryMovementState moveState && moveState.IsFinished)
        {
            SwitchState(ResolveGroundState());
            return;
        }
    }

    private void HandleHitRequest(GameObject attacker)
    {
        var category = CurrentState?.Category ?? StateCategory.Locomotion;

        // Chỉ Override mới không bị hit interrupt
        if (category == StateCategory.Override) return;
        if (CurrentState is PlayerDeadState) return;

        // Locomotion và Combat đều bị hit interrupt
        // (trừ Dash — i-frame)
        if (CurrentState is PlayerDashState) return;
        if (CurrentState is PlayerParryState) return;      // parry window = i-frame
        if (CurrentState is PlayerParryMovementState) return; // parry movement = i-frame

        ForceSwitchState(States.Hit());
    }

    private void HandleParryRequest()
    {
        if (CurrentState is PlayerDeadState) return;
        if (CurrentState is PlayerParryState) return;

        skillManager?.ForceCancelCombo(false);
        ForceSwitchState(States.Parry());
    }

    private void HandleAttackStateRequest()
    {
        var category = CurrentState?.Category ?? StateCategory.Locomotion;

        // Combat không cho vào Attack (tránh override combo đang chạy)
        if (category == StateCategory.Combat) return;

        // Override không cho vào Attack
        if (category == StateCategory.Override) return;

        SwitchState(States.Attack());
    }

    private void HandleDashStateRequest()
    {
        if (!TryDash) return;

        var category = CurrentState?.Category ?? StateCategory.Locomotion;

        // Override không nhận dash
        if (category == StateCategory.Override) return;

        // === CASE 1: Đang ở Grounded → Dash là child ===
        if (CurrentState is PlayerGroundedState groundedState)
        {
            groundedState.SetChildState(States.Dash());
            return;
        }

        // === CASE 2: Đang ở Airborne (Jump/Fall) → AirDash là child ===
        if (CurrentState is PlayerAirborneState airborneState)
        {
            airborneState.SetChildState(States.AirDash());
            return;
        }

        // === CASE 3: Đang ở Combat (Attack) → về Grounded trước, rồi Dash ===
        if (category == StateCategory.Combat)
        {
            // Force về Grounded (sẽ tự InitializeSubState → Idle/Run)
            ForceSwitchState(States.Grounded());

            // Grounded giờ là CurrentState → enter Dash ngay
            // Không cần chờ frame — vì đã ForceSwitchState → CurrentState = Grounded
            if (CurrentState is PlayerGroundedState gState)
            {
                gState.SetChildState(States.Dash());
            }
            return;
        }

        // === Fallback: switch root (không nên xảy ra) ===
        Debug.LogWarning($"[PlayerController] HandleDashStateRequest fallback từ {CurrentState?.GetType().Name}");
        SwitchState(States.Grounded());
    }
    private void HandleJumpStateRequest()
    {
        if (!TryJump) return;

        var category = CurrentState?.Category ?? StateCategory.Locomotion;

        if (category == StateCategory.Override) return;

        SwitchState(States.Jump());
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
        CurrentState.EnterState();
    }

    private void ForceSwitchState(PlayerBaseState newState)
    {
        if (newState == null) return;

        CurrentState?.ForceExit();
        CurrentState = newState;
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
   /// <summary>
/// Public switch cho state gọi khi cần. Dùng bởi PopToParent khi không có parent.
/// </summary>
public void SwitchStateExternal(PlayerBaseState newState)
{
    SwitchState(newState);
}
    private void InitializeServices()
    {
        _physicsHandler = new GravityHandler(_gravity, GravityScaling, FallClamp);
        _rotationHandler = new ModelRotationHandler();

        _animationHandler = new MovementAnimationHandler(
            Animator,
            ID_Idle,
            Anim_Run_F, Anim_Run_B, Anim_Run_L, Anim_Run_R,
            Anim_Run_FL, Anim_Run_FR, Anim_Run_BL, Anim_Run_BR);

        _inputHandler = new CameraRelativeInputHandler(MainCamera, _playerInputs);

        _groundMovementHandler = new ResponsiveMovementHandler(RunMaxSpeed, TAttack);
        _airMovementHandler = new ResponsiveMovementHandler(RunMaxSpeed, TAttack * 1.5f);
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

        return States.Fall();
    }

    #endregion

    #region MOVEMENT

    private void ApplyGravity()
    {
        if (_isInParryMovement)
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
        if (_isInParryMovement)
        {
            _charController.Move(_parryMovementVelocity * Time.deltaTime);
            return;
        }

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
            _appliedMovement = Vector3.zero;
            return;
        }

        Vector3 motion = _appliedMovement + new Vector3(0f, _jumpVelocity, 0f);
        _charController.Move(motion * Time.deltaTime);
        _appliedMovement = Vector3.zero;

        _velocity.x = Mathf.MoveTowards(_velocity.x, 0f, Friction * Time.deltaTime);
        _velocity.z = Mathf.MoveTowards(_velocity.z, 0f, Friction * Time.deltaTime);
    }

    #endregion

    #region ROTATION

    public void RotateModel(Vector3 direction)
    {
        if (Model == null || _rotationLocked) return;
        if (direction.sqrMagnitude < 0.001f) return;

        Model.rotation = Quaternion.LookRotation(direction.normalized);
    }

    public void RotateTowardDirection(Vector3 direction, float speed)
    {
        if (Model == null || _rotationLocked) return;
        if (direction.sqrMagnitude < 0.001f) return;

        _rotationHandler?.RotateTowardDirection(Model, direction, speed);
    }

    public void HandleRotation()
    {
        if (_rotationLocked) return;
        if (_inputVector.sqrMagnitude <= 0.01f) return;

        Vector3 moveDirection = GetLookDirection();
        _rotationHandler?.RotateTowardDirection(Model, moveDirection, RotationSpeed);
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
                      $"<b>ParryMove:</b> {_isInParryMovement} | " +
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

    #region LEGACY

    public void TryEnterParryState() => RequestState(StateRequestType.Parry);
    public void TryEnterHitState() => RequestState(StateRequestType.Hit);

    #endregion
}