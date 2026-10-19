using UnityEngine;
/// <summary>
/// Advanced lunge — lao tới vị trí offset để vào tầm đánh.
/// 
/// CÁCH DÙNG:
///   1. Gắn vào Player (có PlayerController)
///   2. Gán CombatDetection (hoặc PhysicsDetection) vào field "Detection"
///   3. Cấu hình attackRange, maxLungeDistance, cooldown
///   4. Gọi TryLunge() từ AnimationEvent / Input / ComboWindow
/// 
/// PIPELINE:
///   - Detection (do bạn config) → tìm target
///   - Tính stop position = target - dir * attackRange
///   - SphereCast check chướng ngại vật
///   - Override movement qua IVelocityProvider
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class SmartLungeController : MonoBehaviour, IVelocityProvider
{
    [Header("Detection — customize range here")]
    [Tooltip("CombatDetection extends PhysicsDetection. Kéo component vào đây để dùng radius riêng.")]
    [SerializeField] private CombatDetection detection;

    [Tooltip("Nếu trống, tự dùng CombatDetection của PlayerController")]
    [SerializeField] private bool autoFindDetection = true;

    [Header("Lunge Target")]
    [Tooltip("Khoảng cách dừng trước mặt kẻ địch (attack range)")]
    [SerializeField] private float attackRange = 1.8f;

    [Tooltip("Khoảng cách lunge tối đa — target xa hơn sẽ không lunge")]
    [SerializeField] private float maxLungeDistance = 8f;

    [Tooltip("Khoảng cách lunge tối thiểu — tránh micro-lunge lãng phí cooldown")]
    [SerializeField] private float minLungeDistance = 0.3f;

    [Header("Lunge Motion")]
    [Tooltip("Thời gian lunge (giây)")]
    [SerializeField] private float lungeDuration = 0.25f;

    [Tooltip("Curve vị trí 0→1. Tangent 2 ở đầu = bật nhanh, tangent 0 ở cuối = dừng mượt")]
    [SerializeField]
    private AnimationCurve positionCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 2f, 2f),
        new Keyframe(1f, 1f, 0f, 0f)
    );

    [Tooltip("Xoay model về phía target ngay khi bắt đầu lunge")]
    [SerializeField] private bool rotateToTarget = true;

    [Header("Safety")]
    [Tooltip("Layer chướng ngại vật — dùng SphereCast tránh lao vào tường")]
    [SerializeField] private LayerMask obstacleLayer = -1;

    [SerializeField] private float castRadius = 0.4f;

    [Tooltip("Nếu bị kẹt quá thời gian này (giây) → hủy lunge")]
    [SerializeField] private float stuckTimeout = 0.15f;

    [Header("Conditions")]
    [Tooltip("Yêu cầu grounded mới lunge")]
    [SerializeField] private bool requireGrounded = true;

    [Header("Cooldown")]
    [SerializeField] private float cooldown = 0.5f;

    [Header("References")]
    [Tooltip("Model để xoay. Nếu trống tự lấy từ PlayerController.Model")]
    [SerializeField] private Transform model;

    [Header("Debug")]
    [SerializeField] private bool drawDebug = true;

    // =========================================================
    // IVelocityProvider — priority 12 > root motion (5) > step (1)
    // =========================================================
    public int Priority => 12;
    public bool IsActive { get; private set; }

    // =========================================================
    // RUNTIME
    // =========================================================
    private PlayerController _player;
    private float _timer;
    private float _cooldownTimer;
    private float _stuckTimer;
    private Vector3 _lungeDir;
    private float _lungeDistance;
    private Vector3 _startPos;
    private Vector3 _lastActualPos;
    private bool _registered;

    // =========================================================
    // LIFECYCLE
    // =========================================================
    private void Awake()
    {
        _player = GetComponent<PlayerController>();

        if (model == null && _player != null)
            model = _player.Model;

        if (detection == null && autoFindDetection && _player != null)
            detection = _player.CombatDetection;

        if (detection == null)
            Debug.LogWarning($"[SmartLunge] {name}: chưa gán Detection — TryLunge sẽ không tìm target!");
    }

    private void Start()
    {
        if (_player != null && !_registered)
        {
            _player.RegisterVelocityProvider(this);
            _registered = true;
        }
    }

    private void Update()
    {
        if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;

        // Hủy lunge nếu state không cho phép
        if (IsActive && !CanLungeNow())
            Cancel();
    }

    // =========================================================
    // PUBLIC API
    // =========================================================

    /// <summary>
    /// Lunge tới kẻ địch gần nhất trong detection range.
    /// Trả về true nếu có target và lunge bắt đầu.
    /// </summary>
    public bool TryLunge()
    {
        if (IsActive || _cooldownTimer > 0f) return false;
        if (!CanLungeNow()) return false;

        Transform target = FindNearestTarget();
        if (target == null) return false;

        return StartLunge(target);
    }

    /// <summary>
    /// Lunge tới target cụ thể (bỏ qua detection).
    /// </summary>
    public bool TryLunge(GameObject target)
    {
        if (target == null) return false;
        if (IsActive || _cooldownTimer > 0f) return false;
        if (!CanLungeNow()) return false;

        return StartLunge(target.transform);
    }

    /// <summary>
    /// Hủy lunge hiện tại (không tính cooldown).
    /// </summary>
    public void Cancel()
    {
        IsActive = false;
        _timer = 0f;
    }

    // =========================================================
    // INTERNAL
    // =========================================================
    private bool CanLungeNow()
    {
        if (_player == null) return false;
        if (requireGrounded && !_player.CharController.isGrounded) return false;
        if (_player.CurrentState is PlayerDeadState) return false;
        if (_player.CurrentState is PlayerHitState) return false;
        return true;
    }

    private Transform FindNearestTarget()
    {
        if (detection == null) return null;

        GameObject target = detection.GetNearestTarget(
            transform.position, transform.forward, gameObject);

        return target != null ? target.transform : null;
    }

    private bool StartLunge(Transform target)
    {
        if (target == null) return false;

        Vector3 origin = transform.position;
        Vector3 targetPos = target.position;

        // Direction (flatten XZ)
        Vector3 dir = targetPos - origin;
        dir.y = 0f;
        float dist = dir.magnitude;

        if (dist <= 0.001f) return false;
        if (dist <= attackRange) return false;                       // đã trong tầm
        if (dist > maxLungeDistance + attackRange) return false;     // quá xa

        dir /= dist;

        // Stop position = target - dir * attackRange  (offset vào tầm đánh)
        Vector3 stopPos = targetPos - dir * attackRange;
        stopPos.y = origin.y;

        Vector3 lungeVec = stopPos - origin;
        lungeVec.y = 0f;
        float lungeDist = lungeVec.magnitude;

        if (lungeDist < minLungeDistance) return false;
        if (lungeDist > maxLungeDistance) lungeDist = maxLungeDistance;

        // SphereCast check chướng ngại vật
        if (obstacleLayer.value != 0)
        {
            Vector3 castOrigin = origin + Vector3.up * 0.5f;
            if (Physics.SphereCast(castOrigin, castRadius, dir, out RaycastHit hit,
                lungeDist + 0.2f, obstacleLayer, QueryTriggerInteraction.Ignore))
            {
                lungeDist = Mathf.Max(0f, hit.distance - 0.05f);
            }
        }

        if (lungeDist < minLungeDistance) return false;

        // Rotate model
        if (rotateToTarget && model != null)
            model.rotation = Quaternion.LookRotation(dir);

        // Bắt đầu
        _lungeDir = dir;
        _lungeDistance = lungeDist;
        _startPos = origin;
        _lastActualPos = origin;
        _timer = 0f;
        _stuckTimer = 0f;
        IsActive = true;

        return true;
    }

    // =========================================================
    // IVelocityProvider
    // =========================================================
    public Vector3 GetVelocityModifier()
    {
        if (!IsActive) return Vector3.zero;

        _timer += Time.deltaTime;
        float progress = Mathf.Clamp01(_timer / lungeDuration);

        if (progress >= 1f)
        {
            EndLunge();
            return Vector3.zero;
        }

        // Stuck detection
        float actualMove = Vector3.Distance(transform.position, _lastActualPos);
        if (actualMove < 0.0005f) _stuckTimer += Time.deltaTime;
        else _stuckTimer = 0f;
        _lastActualPos = transform.position;

        if (_stuckTimer > stuckTimeout)
        {
            EndLunge();
            return Vector3.zero;
        }

        // Position-based movement theo curve
        float t = positionCurve.Evaluate(progress);
        Vector3 desiredPos = _startPos + _lungeDir * (_lungeDistance * t);
        Vector3 delta = desiredPos - transform.position;
        delta.y = 0f;

        if (Time.deltaTime < 0.0001f) return Vector3.zero;

        // PlayerController gọi Move(velocity * dt) → trả velocity = delta/dt
        Vector3 velocity = delta / Time.deltaTime;

        // Clamp tránh tunneling nếu bị kẹt
        float maxSpeed = _lungeDistance / lungeDuration * 4f;
        if (velocity.sqrMagnitude > maxSpeed * maxSpeed)
            velocity = velocity.normalized * maxSpeed;

        return velocity;
    }

    private void EndLunge()
    {
        IsActive = false;
        _timer = 0f;
        _cooldownTimer = cooldown;
    }

    // =========================================================
    // GIZMOS
    // =========================================================
    private void OnDrawGizmosSelected()
    {
        if (!drawDebug) return;

        // Attack range
        Gizmos.color = new Color(1f, 0.8f, 0f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // Lunge path
        if (IsActive)
        {
            Gizmos.color = Color.red;
            Vector3 end = _startPos + _lungeDir * _lungeDistance;
            Gizmos.DrawLine(_startPos, end);
            Gizmos.DrawWireSphere(end, 0.3f);
            Gizmos.DrawWireSphere(_startPos, 0.2f);
        }
    }
}