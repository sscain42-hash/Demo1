using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : Damageable, IDamageProvider, IPlayerCombatEvents
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
    [field: SerializeField] public Transform Model { get; private set; }
    [field: SerializeField] public Transform MainCamera { get; private set; }
    [field: SerializeField] public Animator Animator { get; private set; }

    [Header("Responsive Movement")]
    [field: SerializeField] public float TAttack { get; private set; } = 0.1f;
    [field: SerializeField] public float TRelease { get; private set; } = 0.15f;

    [Header("Knockback")]
    public float knockBackForce = 15f;
    public AnimationCurve knockBackcurve;

    [Header("References")]
    public GameObject _weaponHitbox;
    public Transform SwordTransform;

    [SerializeField] private CharacterEffect characterEffect;
    [SerializeField] private PlayerSkillManager skillManager;

    public SO_PlayerConfiguration PlayerConfig;

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

    // 🔥 Frame counter cho grounded state
    private int _groundedFrameCount;
    private int _airborneFrameCount;
    private const int GROUNDED_STABLE_FRAMES = 3;

    private bool _isDashing;
    private bool _isAttacking;
    private bool _rotationLocked;

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

    // 🔥 Grounded — raw từ CharacterController
    public bool IsGroundedRaw => _charController.isGrounded;

    // 🔥 Grounded — stable (3 frame liên tiếp), dùng cho STATE LOGIC
    public bool IsGroundedStable => _groundedFrameCount >= GROUNDED_STABLE_FRAMES;
    public bool IsAirborneStable => _airborneFrameCount >= GROUNDED_STABLE_FRAMES;

    public CharacterEffect CharacterEffect { get => characterEffect; set => characterEffect = value; }

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

    private void HandleAttackRequest() => CurrentState?.SwitchState(States.Attack());
    private void HandleDashCancelRequest() => CurrentState?.SwitchState(States.Dash());
    private void HandleJumpCancelRequest() => CurrentState?.SwitchState(States.Jump());

    #endregion

    #region UNITY LIFECYCLE

    private void Awake()
    {
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
        ReadInput();
        UpdateTimers();
        UpdateFrameCounters();       // 🔥 Chạy TRƯỚC state machine

        CurrentState?.UpdateStates();

        ApplyGravity();
        ApplyMovement();
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
            Animator = GetComponentInChildren<Animator>();
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
        // Jump buffer
        if (_playerInputs != null && _playerInputs.JumpHeld)
            _jumpBufferCounter = JumpBufferTime;
        else
            _jumpBufferCounter = Mathf.Max(0f, _jumpBufferCounter - Time.deltaTime);

        // Coyote
        if (_charController.isGrounded)
            _coyoteCounter = CoyoteTime;
        else
            _coyoteCounter = Mathf.Max(0f, _coyoteCounter - Time.deltaTime);

        // Dash cooldown
        if (_dashCooldownTimer > 0f)
            _dashCooldownTimer -= Time.deltaTime;
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

    /// <summary>
    /// 🔥 NGUỒN CHÂN LÝ DUY NHẤT.
    /// Mọi state gọi hàm này khi cần thoát — không tự check isGrounded.
    /// </summary>
    public PlayerBaseState ResolveGroundState()
    {
        // 🔥 AGGRESSIVE CHECK: If CharController.isGrounded = true, player SHOULD be grounded
        // Don't worry too much about velocity - if player is on ground, they're grounded
        if (_charController.isGrounded)
        {
            // Allow small upward velocity from attack animations
            if (_jumpVelocity < 3f)
                return States.Grounded();

            // Even with higher velocity, if grounded, still return Grounded
            // (player will auto-correct via gravity next frame)
            if (_jumpVelocity < 5f && IsGroundedStable)
                return States.Grounded();
        }

        // 🔥 PRIORITY 2: IsGroundedStable (player was grounded for 3+ frames)
        if (IsGroundedStable) 
            return States.Grounded();

        // 🔥 PRIORITY 3: Recently grounded (within 1 frame) + minimal velocity
        if (_groundedFrameCount > 0 && _jumpVelocity < 1f)
            return States.Grounded();

        // Otherwise falling
        return States.Fall();
    }
    #endregion

    #region MOVEMENT

    /// <summary>
    /// 🔥 Vật lý — DÙNG isGrounded RAW, KHÔNG dùng stable.
    /// Stable chỉ dành cho state logic.
    /// </summary>
    private void ApplyGravity()
    {
        if (_charController.isGrounded && _jumpVelocity < 0f && !_isDashing)
        {
            // Stick force — set một lần, không tích lũy
            _jumpVelocity = -2f;
        }
        else
        {
            // Rơi tự do
            _jumpVelocity += _gravity * GravityScaling * Time.deltaTime;
            _jumpVelocity = Mathf.Max(_jumpVelocity, FallClamp);
        }
    }

    private void ApplyMovement()
    {
        // Tìm provider có priority cao nhất
        IVelocityProvider bestProvider = null;
        int highestPriority = -1;

        for (int i = 0; i < _velocityProviders.Count; i++)
        {
            var provider = _velocityProviders[i];
            if (provider != null && provider.IsActive && provider.Priority > highestPriority)
            {
                bestProvider = provider;
                highestPriority = provider.Priority;
            }
        }

        if (bestProvider != null)
        {
            // 🔥 FIX: GetVelocityModifier() trả về VELOCITY (units/sec)
            // Phải nhân Time.deltaTime để chuyển thành displacement cho Move()
            Vector3 motion = bestProvider.GetVelocityModifier() * Time.deltaTime;
            _charController.Move(motion);
            _velocity = Vector3.zero;
            _appliedMovement = Vector3.zero;
            return;
        }

        Vector3 normalMotion = _appliedMovement + new Vector3(0f, _jumpVelocity, 0f);
        _charController.Move(normalMotion * Time.deltaTime);

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

    #region COMBAT

    public void SetAttackLock(bool value) => _isAttacking = value;
    public void SetRotationLock(bool value) => _rotationLocked = value;
    public void ResetDashCooldown() => _dashCooldownTimer = DashCooldown;

    public override void CauseDMG(GameObject target, AttackType attackType)
    {
        if (!DamageableData.Contains(target, out var receiver))
            return;

        receiver.TakeDMG(100, true);

        var knockbackTarget = target.GetComponent<IKnockbackable>();
        if (knockbackTarget != null)
        {
            Vector3 dir = GetLookDirection();
            dir.y = 0;
            knockbackTarget.OnKnockback(dir, knockBackForce, knockBackcurve);
        }

        if (DMGPopUpGenerator.Instance != null)
            DMGPopUpGenerator.Instance.Create(target.transform.position, 100, false, true);
    }

    public void ExecuteDamage(GameObject victim, AttackType attackType) => CauseDMG(victim, attackType);

    #endregion

    #region UTILITIES

    public Vector3 GetLookDirection()
    {
        Vector3 dir = _inputHandler.GetMovementDirection(_inputVector);
        return dir == Vector3.zero ? Model.forward : dir;
    }

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
    private void OnGUI()
    {
        if (CurrentState == null) return;

        GUIStyle style = new GUIStyle(GUI.skin.box)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        style.normal.textColor = Color.yellow;

        string stateText = GetFormattedStateName(CurrentState);
        string info = $"<b>State:</b> {stateText}\n" +
                      $"<b>Raw:</b> {IsGroundedRaw} | " +
                      $"<b>Stable:</b> {IsGroundedStable} | " +
                      $"<b>G:</b> {_groundedFrameCount} | " +
                      $"<b>A:</b> {_airborneFrameCount}\n" +
                      $"<b>JumpVel:</b> {_jumpVelocity:F2} | " +
                      $"<b>PosY:</b> {transform.position.y:F4}";

        GUI.Box(new Rect(10, 10, 500, 90), info, style);
    }

    private string GetFormattedStateName(PlayerBaseState state)
    {
        if (state == null) return "None";
        string name = state.GetType().Name;
        if (state.ChildState != null)
            name += " → " + GetFormattedStateName(state.ChildState);
        return name;
    }
}
