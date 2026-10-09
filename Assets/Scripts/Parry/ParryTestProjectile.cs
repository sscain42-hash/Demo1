using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Viên đạn test Parry — dùng Rigidbody để bay.
/// Hỗ trợ HOMING (tự bám Player).
/// 
/// SETUP PREFAB:
/// 1. Tạo Sphere/Cube nhỏ (scale ~0.3)
/// 2. Add Rigidbody:
///    - Use Gravity: ☐ (bỏ tick)
///    - Collision Detection: Continuous Dynamic
///    - Interpolate: Interpolate
/// 3. Add Collider (Sphere/Capsule):
///    - Is Trigger: ☐ (để va chạm vật lý)
/// 4. Add Reference component (cho pool)
/// 5. Add script này
/// 6. Set Tag = "Enemy"
/// 7. Save thành prefab
/// 
/// HOMING MODES:
/// - None: Bay thẳng, không bám
/// - Weak: Bám nhẹ (turn rate thấp) — dễ né
/// - Medium: Bám vừa — cân bằng
/// - Strong: Bám gắt (turn rate cao) — khó né
/// - Perfect: Bám tuyệt đối (luôn hướng tới target)
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class ParryTestProjectile : Damageable
{
    public enum HomingMode
    {
        None,       // Không bám
        Weak,       // Bám nhẹ
        Medium,     // Bám vừa
        Strong,     // Bám gắt
        Perfect     // Bám tuyệt đối
    }

    [Header("=== DAMAGE ===")]
    [Tooltip("Sát thương gây ra khi trúng Player")]
    [SerializeField] private int damageAmount = 50;

    [Tooltip("Tỉ lệ crit (0-1). 0 = không bao giờ crit")]
    [Range(0f, 1f)]
    [SerializeField] private float critChance = 0f;

    [Tooltip("Hệ số crit damage. 1.5 = +50% damage khi crit")]
    [SerializeField] private float critMultiplier = 1.5f;

    [Header("=== ATTACK PROPERTIES ===")]
    [Tooltip("Đòn này có thể bị Parry không? (Đòn đỏ = false)")]
    [SerializeField] private bool canBeParried = true;

    [Header("=== HOMING SETTINGS ===")]
    [Tooltip("Chế độ homing")]
    [SerializeField] private HomingMode homingMode = HomingMode.None;

    [Tooltip("Turn rate (độ/giây) — chỉ dùng cho Weak/Medium/Strong")]
    [SerializeField] private float turnRate = 180f;

    [Tooltip("Khoảng cách tối đa để bắt đầu homing (m). Xa hơn → bay thẳng")]
    [SerializeField] private float homingRange = 20f;

    [Tooltip("Tag của target để bám")]
    [SerializeField] private string homingTargetTag = "Player";

    [Tooltip("Tự tìm target khi spawn")]
    [SerializeField] private bool autoFindTarget = true;

    [Tooltip("Thời gian delay trước khi bắt đầu homing (giây)")]
    [SerializeField] private float homingDelay = 0f;

    [Header("=== LIFETIME ===")]
    [Tooltip("Thời gian sống tối đa (giây)")]
    [SerializeField] private float lifetime = 5f;

    [Tooltip("Tự hủy sau khi gây damage thành công")]
    [SerializeField] private bool destroyOnHit = true;

    [Tooltip("Tự hủy khi bị parry")]
    [SerializeField] private bool destroyOnParry = true;

    [Tooltip("Delay trước khi destroy (giây)")]
    [SerializeField] private float destroyDelay = 0.05f;

    [Header("=== FEEDBACK (tùy chọn) ===")]
    [SerializeField] private Reference parriedVFXPrefab;
    [SerializeField] private AudioClip parriedSFX;
    [SerializeField] private Reference hitVFXPrefab;
    [SerializeField] private AudioClip hitSFX;

    [Header("=== DEBUG ===")]
    [SerializeField] private bool debugLog = false;

    [Tooltip("Vẽ đường homing trong Scene")]
    [SerializeField] private bool drawHomingGizmos = true;

    [Header("=== EVENTS ===")]
    public UnityEvent<GameObject> OnDamageDealt;
    public UnityEvent<GameObject> OnParried;

    // === RUNTIME ===
    private Rigidbody _rb;
    private Collider _collider;
    private Transform _homingTarget;
    private float _spawnTime;
    private float _speed;
    private bool _hasHit;
    private bool _hasParried;
    private bool _hasLaunched;
    private Vector3 _initialDirection;

    // =========================================================
    // UNITY LIFECYCLE
    // =========================================================
    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _collider = GetComponent<Collider>();

        _rb.useGravity = false;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        _spawnTime = Time.time;
        _hasHit = false;
        _hasParried = false;
        _hasLaunched = false;

        if (_rb != null)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic = false;
        }

        if (_collider != null) _collider.enabled = true;
    }

    private void Update()
    {
        // Tự hủy sau lifetime
        if (Time.time - _spawnTime >= lifetime)
        {
            if (debugLog) Debug.Log($"[Projectile] {name} hết lifetime — tự hủy");
            DestroyProjectile();
            return;
        }

        // Homing logic
        if (_hasLaunched && CanHoming())
        {
            ApplyHoming();
        }
    }

    // =========================================================
    // LAUNCH — Gọi từ Turret
    // =========================================================
    public void Launch(Vector3 direction, float speed)
    {
        if (_rb == null) _rb = GetComponent<Rigidbody>();

        _speed = speed;
        _initialDirection = direction.normalized;
        _hasLaunched = true;

        _rb.linearVelocity = _initialDirection * _speed;

        if (_initialDirection.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(_initialDirection);

        // Tìm target ngay khi launch
        if (autoFindTarget)
            FindTarget();
    }

    // =========================================================
    // HOMING LOGIC
    // =========================================================
    private bool CanHoming()
    {
        if (homingMode == HomingMode.None) return false;
        if (_homingTarget == null) return false;
        if (!_homingTarget.gameObject.activeInHierarchy) return false;

        // Delay sau khi spawn
        if (Time.time - _spawnTime < homingDelay) return false;

        // Range check
        float distance = Vector3.Distance(transform.position, _homingTarget.position);
        if (distance > homingRange) return false;

        return true;
    }

    private void ApplyHoming()
    {
        Vector3 desiredDir = (_homingTarget.position - transform.position).normalized;

        if (homingMode == HomingMode.Perfect)
        {
            // Bám tuyệt đối — luôn hướng thẳng tới target
            _rb.linearVelocity = desiredDir * _speed;
            transform.rotation = Quaternion.LookRotation(desiredDir);
            return;
        }

        // Các mode còn lại: dùng turn rate
        float turnSpeed = GetTurnRate();
        Vector3 currentDir = _rb.linearVelocity.normalized;

        Vector3 newDir = Vector3.RotateTowards(
            currentDir,
            desiredDir,
            turnSpeed * Mathf.Deg2Rad * Time.deltaTime,
            0f
        );

        // Áp dụng hướng mới
        _rb.linearVelocity = newDir * _speed;

        // Xoay model theo hướng bay
        if (newDir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(newDir);
    }

    private float GetTurnRate()
    {
        switch (homingMode)
        {
            case HomingMode.Weak: return turnRate * 0.3f;    // 30% turn rate
            case HomingMode.Medium: return turnRate;          // 100%
            case HomingMode.Strong: return turnRate * 2f;     // 200%
            case HomingMode.Perfect: return 99999f;           // Không giới hạn
            default: return turnRate;
        }
    }

    private void FindTarget()
    {
        if (!string.IsNullOrEmpty(homingTargetTag))
        {
            var targetObj = GameObject.FindGameObjectWithTag(homingTargetTag);
            if (targetObj != null)
            {
                _homingTarget = targetObj.transform;
                if (debugLog)
                    Debug.Log($"[Projectile] Found target: {targetObj.name}");
            }
        }
    }

    /// <summary>
    /// Set target thủ công từ ngoài (turret có thể truyền vào).
    /// </summary>
    public void SetHomingTarget(Transform target)
    {
        _homingTarget = target;
    }

    // =========================================================
    // COLLISION DETECTION
    // =========================================================
    private void OnCollisionEnter(Collision collision)
    {
        Vector3 hitPoint = collision.contactCount > 0
            ? collision.GetContact(0).point
            : transform.position;

        HandleHit(collision.gameObject, hitPoint);
    }

    private void OnTriggerEnter(Collider other)
    {
        Vector3 hitPoint = other.ClosestPoint(transform.position);
        HandleHit(other.gameObject, hitPoint);
    }

    private void HandleHit(GameObject target, Vector3 hitPosition)
    {
        if (_hasHit) return;
        if (target == null) return;

        // Nếu va vào target đang homing → xử lý damage/parry
        if (_homingTarget != null && target == _homingTarget.gameObject)
        {
            _hasHit = true;
            if (debugLog) Debug.Log($"[Projectile] Va chạm target {target.name}");
            CauseDMG(target, AttackType.NormalAttack);
            return;
        }

        // Nếu va vào Player (kể cả không phải homing target)
        if (target.CompareTag("Player"))
        {
            _hasHit = true;
            CauseDMG(target, AttackType.NormalAttack);
            return;
        }

        // Va vào vật khác → hủy
        if (debugLog) Debug.Log($"[Projectile] Va vào {target.name} — tự hủy");
        DestroyProjectile();
    }

    // =========================================================
    // CORE — Gây damage + Parry check
    // =========================================================
    public override void CauseDMG(GameObject target, AttackType type)
    {
        if (target == null) return;

        // Check Parry
        if (canBeParried)
        {
            var parryReceiver = target.GetComponent<IParryReceiver>();
            if (parryReceiver != null && parryReceiver.TryParry(gameObject))
            {
                HandleParried(target);
                if (destroyOnParry) DestroyProjectile();
                return;
            }
        }
        else
        {
            if (debugLog)
                Debug.Log($"[Projectile] {name} là đòn KHÔNG THỂ PARRY");
        }

        // Gây damage
        if (!DamageableData.Contains(target, out var receiver))
        {
            if (debugLog) Debug.LogWarning($"[Projectile] {target.name} không có Damageable!");
            return;
        }

        bool isCrit = Random.value < critChance;
        int finalDamage = isCrit
            ? Mathf.RoundToInt(damageAmount * critMultiplier)
            : damageAmount;

        receiver.TakeDMG(finalDamage, isCrit);

        if (debugLog)
            Debug.Log($"[Projectile] {target.name} nhận {finalDamage} damage (crit={isCrit})");

        PlayHitFeedback(target);
        OnDamageDealt?.Invoke(target);

        if (destroyOnHit) DestroyProjectile();
    }

    public void CauseDMG(GameObject target) => CauseDMG(target, AttackType.NormalAttack);

    // =========================================================
    // PARRY HANDLER
    // =========================================================
    private void HandleParried(GameObject parrier)
    {
        _hasParried = true;

        if (debugLog)
            Debug.Log($"[Projectile] {name} bị PARRY bởi {parrier.name}!");

        // Dừng di chuyển
        if (_rb != null)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic = true;
        }

        if (_collider != null) _collider.enabled = false;

        PlayParriedFeedback(parrier);
        OnParried?.Invoke(parrier);
    }

    // =========================================================
    // FEEDBACK
    // =========================================================
    private void PlayHitFeedback(GameObject target)
    {
        if (hitVFXPrefab != null)
        {
            Vector3 pos = target.transform.position + Vector3.up * 1.2f;
            Vector3 dir = (target.transform.position - transform.position).normalized;
            Quaternion rot = dir.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(dir)
                : Quaternion.identity;

            GlobalVFXManager.SpawnGlobalVFX(hitVFXPrefab, pos, rot);
        }

        if (hitSFX != null && SoundManager.Instance != null)
            SoundManager.Instance.PlaySFXAtPosition(hitSFX, target.transform.position);
    }

    private void PlayParriedFeedback(GameObject parrier)
    {
        if (parriedVFXPrefab != null)
        {
            Vector3 pos = transform.position;
            Vector3 dir = (parrier.transform.position - transform.position).normalized;
            dir.y = 0;
            Quaternion rot = dir.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(dir)
                : Quaternion.identity;

            GlobalVFXManager.SpawnGlobalVFX(parriedVFXPrefab, pos, rot);
        }

        if (parriedSFX != null && SoundManager.Instance != null)
            SoundManager.Instance.PlaySFXAtPosition(parriedSFX, transform.position);
    }

    // =========================================================
    // CLEANUP
    // =========================================================
    private void DestroyProjectile()
    {
        if (_rb != null)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        if (TryGetComponent(out Reference reference))
        {
            if (destroyDelay > 0f)
                Invoke(nameof(ReleaseToPool), destroyDelay);
            else
                ReleaseToPool();
        }
        else
        {
            Destroy(gameObject, destroyDelay);
        }
    }

    private void ReleaseToPool()
    {
        if (TryGetComponent(out Reference reference))
            reference.Release();
    }

    // =========================================================
    // GIZMOS
    // =========================================================
    private void OnDrawGizmosSelected()
    {
        if (!drawHomingGizmos) return;

        // Vẽ hướng bay hiện tại
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
        if (Application.isPlaying && _rb != null)
        {
            Gizmos.DrawRay(transform.position, _rb.linearVelocity.normalized * 3f);
        }

        // Vẽ đường tới target khi đang homing
        if (Application.isPlaying && _homingTarget != null && homingMode != HomingMode.None)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, _homingTarget.position);
            Gizmos.DrawWireSphere(_homingTarget.position, 0.5f);

            // Vẽ vùng homing range
            Gizmos.color = new Color(1f, 0f, 0f, 0.15f);
            Gizmos.DrawWireSphere(transform.position, homingRange);
        }
    }
}