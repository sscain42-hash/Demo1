using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : Damageable
{
    #region CONFIG

    [Header("GDC 2016 Constants")]
    [field: SerializeField] public float JumpHeight { get; private set; } = 4f;
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
    public AnimationCurve DashCurve { get; private set; } =
        AnimationCurve.Linear(0, 1, 1, 0);

    [Header("Movement & Rotation")]
    [field: SerializeField] public float RotationSpeed { get; private set; } = 15f;
    [field: SerializeField] public float AirControl { get; private set; } = 5f;
    [field: SerializeField] public Transform Model { get; private set; }
    [field: SerializeField] public Transform MainCamera { get; private set; }
    [field: SerializeField] public Animator Animator { get; private set; }

    [Header("Slope Detection")]
    [field: SerializeField] public float MaxSlopeAngle { get; private set; } = 45f;
    [field: SerializeField] public float SlopeCheckDistance { get; private set; } = 0.5f;

    [Header("Responsive Movement")]
    [field: SerializeField] public float TAttack { get; private set; } = 0.1f;
    [field: SerializeField] public float TRelease { get; private set; } = 0.15f;




    [Header("Lunge")]
    public float offsetLunge = 1f;
    public float lungeRange = 3f;
    public float attackRange = 2f;

    [SerializeField]
    private AnimationCurve _lungeCurve =
        AnimationCurve.EaseInOut(0, 0, 1, 1);

    [SerializeField]
    private float lungeSpd = 6f;

    public GameObject _weaponHitbox;

    #endregion

    #region CONSTANTS

    private const float MIN_DISTANCE_THRESHOLD = 0.01f;
    private const float LUNGE_TOLERANCE = 0.05f;

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

    private ResponsiveDecelerationHandler _decelerationHandler;

    #endregion

    #region STATE MACHINE

    private PlayerStateFactory _states;
    private PlayerBaseState _currentState;

    #endregion

    #region RUNTIME STATE

    private Vector3 _velocity;
    private Vector2 _inputVector;

    private float _gravity;
    private float _initialJumpVelocity;

    private float _coyoteCounter;
    private float _jumpBufferCounter;
    private float _dashCooldownTimer;

    private bool _isDashing;
    private bool _isAttacking;
    private bool _attackLocked;
    private bool _rotationLocked;
    private bool _wasGroundedLastFrame;

    private bool _isOnSlope;
    private float _currentSlopeAngle;
    private Vector3 _lastGroundNormal = Vector3.up;

    private int _skillCooldown;
    private int _burstCooldown;

    private Coroutine _lungeRoutine;

    #endregion

    #region INPUT

    public PlayerInputs _playerInputs;

    #endregion

    #region PROPERTIES

    public CharacterController CharController => _charController;

    public PlayerBaseState CurrentState
    {
        get => CurrentState1;
        set => CurrentState1 = value;
    }

    public Vector3 Velocity
    {
        get => _velocity;
        set => _velocity = value;
    }

    public Vector2 InputVector => _inputVector;

    public float Gravity => _gravity;
    public float InitialJumpVelocity => _initialJumpVelocity;

    public Vector3 GroundNormal => _lastGroundNormal;
    public bool IsOnSlope => _isOnSlope;
    public float CurrentSlopeAngle => _currentSlopeAngle;

    public IPhysicsHandler PhysicsHandler => _physicsHandler;
    public IRotationHandler RotationHandler => _rotationHandler;
    public IAnimationHandler AnimationHandler => _animationHandler;
    public IInputHandler InputHandler => _inputHandler;
    public IMovementHandler GroundMovementHandler => _groundMovementHandler;
    public IMovementHandler AirMovementHandler => _airMovementHandler;
    #region Velocity Providers

    private List<IVelocityProvider> _velocityProviders = new List<IVelocityProvider>();

    // Đăng ký/Hủy đăng ký các nguồn lực
    public void RegisterVelocityProvider(IVelocityProvider provider) => _velocityProviders.Add(provider);
    public void UnregisterVelocityProvider(IVelocityProvider provider) => _velocityProviders.Remove(provider);
    #endregion
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

    #endregion

    #region ANIMATION HASH

    public readonly int IDVertical = Animator.StringToHash("Vertical");
    public readonly int IDHorizontal = Animator.StringToHash("Horizontal");
    public readonly int IDSpeed = Animator.StringToHash("Speed");
    public readonly int IDJump = Animator.StringToHash("Jump");
    public readonly int IDFall = Animator.StringToHash("Fall");
    public readonly int IDDash = Animator.StringToHash("Dash");

    public readonly int Anim_Idle = Animator.StringToHash("HumanM@Idle01");
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

    #endregion

    [SerializeField] private CharacterEffect characterEffect;
    public CharacterEffect CharacterEffect { get => characterEffect; set => characterEffect = value; }

    #region UNITY METHODS

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
        CurrentState1 = States.Grounded();
        CurrentState1.EnterState();

        _wasGroundedLastFrame = _charController.isGrounded;

    
        Animator.applyRootMotion = true;
    }

    private void Update()
    {
        ReadInput();

        UpdateTimers();

        UpdateStateMachine();

        HandleRotation();

        ApplyMovement();

        CacheFrameState();
    }

    private void OnAnimatorMove()
    {

        // Nếu không trong trạng thái khóa tấn công hoặc không áp dụng Root Motion, thoát ra
        if (!_attackLocked || !Animator.applyRootMotion)
            return;

        // 1. Lấy khoảng cách di chuyển từ Animation
        Vector3 finalDelta = Animator.deltaPosition;

        // 2. Trộn lực Step mà PlayerAttackState vừa nạp vào _velocity ở trên
        if (_velocity.sqrMagnitude > 0.001f)
        {
            // Cộng thêm quãng đường từ Code (Vận tốc * Thời gian) vào hướng X và Z
            finalDelta.x += _velocity.x * Time.deltaTime;
            finalDelta.z += _velocity.z * Time.deltaTime;

            // Giảm dần lực Step (Ma sát hãm phanh) dựa trên chỉ số Friction có sẵn của bạn
            float verticalVelocity = _velocity.y; // Giữ lại trọng lực Y
            _velocity = Vector3.MoveTowards(_velocity, Vector3.zero, Friction * Time.deltaTime);
            _velocity.y = verticalVelocity;
        }

        // 3. Thực thi duy nhất một lệnh Move tại đây cho toàn bộ trạng thái Attack
        _charController.Move(finalDelta);
  
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.black;
        GizmoUtils.DrawCircle(transform.position, lungeRange);
    }

    #endregion

    #region INITIALIZATION

    private void InitializeComponents()
    {
        _charController = GetComponent<CharacterController>();
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
        _physicsHandler =
            new GravityHandler(_gravity, GravityScaling, FallClamp);

        _rotationHandler =
            new ModelRotationHandler();

        _animationHandler =
            new MovementAnimationHandler(
                Animator,
                Anim_Idle,
                Anim_Run_F,
                Anim_Run_B,
                Anim_Run_L,
                Anim_Run_R,
                Anim_Run_FL,
                Anim_Run_FR,
                Anim_Run_BL,
                Anim_Run_BR);

        _inputHandler =
            new CameraRelativeInputHandler(MainCamera);

        _groundMovementHandler =
            new ResponsiveMovementHandler(
                RunMaxSpeed,
                TAttack);
        var comboManager = GetComponent<PlayerActionComboManager>();
        _airMovementHandler =
            new ResponsiveMovementHandler(
                RunMaxSpeed,
                TAttack * 1.5f);

        _decelerationHandler =
            new ResponsiveDecelerationHandler(
                RunMaxSpeed,
                TRelease);


      
    }

    private void ComputePhysicsConstants()
    {
        _gravity =
            -(2f * JumpHeight) /
            Mathf.Pow(TimeToJumpApex, 2f);

        _initialJumpVelocity =
            Mathf.Abs(_gravity) *
            TimeToJumpApex;
    }

    #endregion

    #region UPDATE FLOW

    private void ReadInput()
    {
        _inputVector =
            _inputHandler.ReadMovementInput();
    }

    private void UpdateStateMachine()
    {
        CurrentState1.UpdateStates();
    }

    private void CacheFrameState()
    {
        _wasGroundedLastFrame =
            _charController.isGrounded;
    }

    #endregion

    #region TIMERS

    private void UpdateTimers()
    {
        UpdateJumpBuffer();
        UpdateCoyoteTime();
        UpdateDashCooldown();
    }

    private void UpdateJumpBuffer()
    {
        if (_playerInputs.JumpHeld)
        {
            _jumpBufferCounter = JumpBufferTime;
            return;
        }

        _jumpBufferCounter =
            Mathf.Max(
                0f,
                _jumpBufferCounter - Time.deltaTime);
    }

    private void UpdateCoyoteTime()
    {
        // 🔥 ĐÃ SỬA: Chuẩn hóa bộ đếm đứt đoạn Coyote
        if (_charController.isGrounded)
        {
            _coyoteCounter = CoyoteTime;
        }
        else
        {
            _coyoteCounter = Mathf.Max(0f, _coyoteCounter - Time.deltaTime);
        }
    }

    private void UpdateDashCooldown()
    {
        if (_dashCooldownTimer <= 0f)
            return;

        _dashCooldownTimer -= Time.deltaTime;
    }

    #endregion

    #region MOVEMENT

    private void ApplyMovement()
    {
        ApplyGravity();
        ApplyGroundSnap();
        MoveCharacter();
        DetectGroundNormal();
    }

    private void ApplyGravity()
    {
        // 🔥 ĐÃ SỬA: Nếu nhân vật lọt chân khỏi vực nhưng vẫn còn Coyote Time ân huệ,
        // chúng ta đóng băng trọng lực kéo tụt tự do để tránh giật khựng (Jitter) hình ảnh.
        if (!_charController.isGrounded && _coyoteCounter > 0f && !_isDashing)
        {
            // Ghim nhẹ vận tốc rơi ở mức cực nhỏ gần như lướt thẳng ra không trung
            _velocity.y = -0.5f;
        }
        else
        {
            _physicsHandler.ApplyGravity(ref _velocity, _isDashing);
        }
    }

    private void ApplyGroundSnap()
    {
        if (!_charController.isGrounded)
            return;

        // 🔥 ĐÃ SỬA: Chỉ kích hoạt GroundSnap ép dính sàn khi không có xu hướng nhảy lên,
        // giúp triệt tiêu xung đột lực kéo thả cục bộ tại biên đa giác mép vực.
        if (_velocity.y <= 0.01f)
        {
            _physicsHandler.ApplyGroundSnap(ref _velocity);
        }
    }

    private void MoveCharacter()
    {
        // 1. TÌM PROVIDER MẠNH NHẤT (Dash/Jump > Combo)
        IVelocityProvider bestProvider = null;
        int highestPriority = -1;

        foreach (var provider in _velocityProviders)
        {
            if (provider.IsActive && provider.Priority > highestPriority)
            {
                bestProvider = provider;
                highestPriority = provider.Priority;
            }
        }

        // 2. NẾU CÓ PROVIDER HOẠT ĐỘNG (Dash, Jump hoặc Combo)
        if (bestProvider != null)
        {
            Vector3 moveStep = bestProvider.GetVelocityModifier();

            // Ghi đè trực tiếp vận tốc
            _charController.Move(moveStep);

            // Reset vận tốc nội tại để không bị cộng dồn ma sát
            _velocity = Vector3.zero;
        }
        // 3. NẾU KHÔNG CÓ CÁI NÀO HOẠT ĐỘNG THÌ MỚI DI CHUYỂN BÌNH THƯỜNG
        else
        {
            _charController.Move(_velocity * Time.deltaTime);
            _velocity.x = Mathf.MoveTowards(_velocity.x, 0, Friction * Time.deltaTime);
            _velocity.z = Mathf.MoveTowards(_velocity.z, 0, Friction * Time.deltaTime);
        }
    }

    private void DetectGroundNormal()
    {
        if (!_charController.isGrounded)
        {
            _isOnSlope = false;
            return;
        }

        RaycastHit hit;

        // Tịnh tiến điểm bắn tia lên cao một chút để tránh việc lọt tia dưới sàn
        Vector3 rayStart =
            transform.position + Vector3.up * 0.1f;

        if (!Physics.Raycast(
                rayStart,
                Vector3.down,
                out hit,
                SlopeCheckDistance))
            return;

        _lastGroundNormal = hit.normal;

        _currentSlopeAngle =
            Vector3.Angle(
                _lastGroundNormal,
                Vector3.up);

        _isOnSlope =
            _currentSlopeAngle > 0.1f &&
            _currentSlopeAngle < MaxSlopeAngle;
    }

    #endregion

    #region ROTATION

    private void HandleRotation()
    {
        if (CurrentState1 is PlayerDashState)
            return;

        if (_rotationLocked)
            return;

        Vector3 moveDirection = GetLookDirection();

        if (_isAttacking)
        {
            _rotationHandler.RotateTowardCamera(
                Model,
                MainCamera,
                RotationSpeed);

            return;
        }

        if (_inputVector.sqrMagnitude <= 0.01f)
            return;

        _rotationHandler.RotateTowardDirection(
            Model,
            moveDirection,
            RotationSpeed);
    }

    private void RotateModel(Vector3 direction)
    {
        if (Model == null)
            return;

        if (direction.sqrMagnitude < 0.001f)
            return;

        Model.rotation =
            Quaternion.LookRotation(direction.normalized);
    }

    #endregion

    #region INPUT ACTIONS
    public bool TryNormalAttack =>
        !_isAttacking &&
        _playerInputs.HasCommand(BufferedAction.NormalAttack);

    public bool TryElementalSkill =>
        _skillCooldown <= 0 &&
        _playerInputs.HasCommand(BufferedAction.ElementalSkill);

    public bool TryElementalBurst =>
        _burstCooldown <= 0 &&
        _playerInputs.HasCommand(
            BufferedAction.ElementalBurst);

    public bool TryDash =>
        _dashCooldownTimer <= 0f &&
        _playerInputs.HasCommand(
            BufferedAction.Dash);


    public bool CanAttack =>
        !_isAttacking &&
        !_attackLocked;

    public PlayerStateFactory States { get => _states; set => _states = value; }
    public PlayerBaseState CurrentState1 { get => _currentState; set => _currentState = value; }

    public SO_PlayerConfiguration PlayerConfig;
   
    #endregion

    #region ANIMATION

    public int GetMovementAnimation()
    {
        return _animationHandler.GetMovementAnimation(
            _inputVector,
            _isAttacking);
    }

    public void PlayAnimation(
        int animHash,
        float transition = 0.1f)
    {
        _animationHandler.PlayAnimation(
            animHash,
            transition);
    }

    public void PlayAnimation(
        string animName,
        float transition = 0.1f)
    {
        _animationHandler.PlayAnimation(
            animName,
            transition);
    }

    #endregion

    #region COMBAT

    public void SetAttackLock(bool value)
    {
        _attackLocked = value;
    }

    public void SetRotationLock(bool value)
    {
        _rotationLocked = value;
    }

    public void ResetDashCooldown()
    {
        _dashCooldownTimer = DashCooldown;
    }

    public override void CauseDMG(
        GameObject target,
        AttackType attackType)
    {
        Debug.Log(
            $"{target.name} is being attacked with {attackType}");

        if (!DamageableData.Contains(target, out var receiver))
            return;

        ApplyHit(attackType);

        receiver.TakeDMG(999, true);
    }
    public override void TakeDMG(int _damage, bool _isCRIT)
    {
        DMGPopUpGenerator.Instance.Create(transform.position, _damage, false, false);
    }
    public void ApplyHit(AttackType type)
    {
        float hitStop = type switch
        {
            AttackType.NormalAttack => 0.1f,
            AttackType.ChargedAttack => 0.06f,
            AttackType.E => 0.08f,
            AttackType.Q => 0.12f,
            _ => 0.03f
        };

        Debug.Log(
            $"Applying hit stop of {hitStop} seconds for {type}");

        HitStopSystem.Instance?.Trigger(hitStop,0.05f);
    }

    #endregion

    #region LUNGE


    public void RotateToTarget(Vector3 target)
    {

        if (_inputVector.sqrMagnitude > 0.01f) return;
        Vector3 direction =
            target - transform.position;

        direction.y = 0f;
        Debug.Log($"Rotating towards target at {target} with direction {direction}");
        RotateModel(direction);
    }

    public void LungeToTarget(GameObject target)
    {
        bool hasInput =
        _inputVector.sqrMagnitude > 0.01f;
        if (hasInput) return;
        if (_lungeRoutine != null)
            StopCoroutine(_lungeRoutine);


        Debug.Log($"Lunging towards target at {target}");
        _lungeRoutine =
            StartCoroutine(
                LungeRoutine(target.transform.position));
    }

    private IEnumerator LungeRoutine(
        Vector3 target)
    {
        while (true)
        {
            Vector3 current =
                transform.position;

            Vector3 direction =
                target - current;

            direction.y = 0f;

            float distance =
                direction.magnitude;

            if (distance <= offsetLunge)
                yield break;

            direction.Normalize();

            Vector3 desiredPosition =
                target -
                direction * offsetLunge;

            Vector3 next =
                Vector3.MoveTowards(
                    current,
                    desiredPosition,
                    lungeSpd *
                    Time.deltaTime);

            Vector3 delta =
                next - current;

            _charController.Move(delta);

            RotateModel(direction);

            yield return null;
        }
    }

    public void StopLunge()
    {
        if (_lungeRoutine == null)
            return;

        StopCoroutine(_lungeRoutine);

        _lungeRoutine = null;
    }

    #endregion

    #region UTILITIES

    public Vector3 GetLookDirection()
    {
        return _inputHandler.GetMovementDirection(_inputVector);
    }

    public Vector3 GetHorizontalDashDirection()
    {
        if (_inputVector.sqrMagnitude < 0.01f)
            return Model ? Model.forward : Vector3.forward;

        Vector3 cameraForward = MainCamera.forward;
        Vector3 cameraRight = MainCamera.right;

        cameraForward.y = 0f;
        cameraRight.y = 0f;

        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 moveDirection =
            cameraForward * _inputVector.y +
            cameraRight * _inputVector.x;

        return moveDirection.normalized;
    }

    public void SetVelocity(float x, float y, float z)
    {
        _velocity = new Vector3(x, y, z);
    }

    public void SetVelocityX(float x)
    {
        _velocity.x = x;
    }

    public void SetVelocityY(float y)
    {
        _velocity.y = y;
    }

    public void SetVelocityZ(float z)
    {
        _velocity.z = z;
    }

    public void AddVelocity(Vector3 delta)
    {
        _velocity += delta;
    }

    #endregion
}
