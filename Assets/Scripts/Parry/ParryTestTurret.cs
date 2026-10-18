using UnityEngine;

/// <summary>
/// Máy bắn test Parry — CHỈ auto fire theo tần suất.
/// Không có manual fire.
/// 
/// SETUP:
/// 1. Tạo Empty GameObject (hoặc Cylinder làm bệ bắn)
/// 2. Gắn script này
/// 3. Gán Projectile Prefab (có Rigidbody + ParryTestProjectile)
/// 4. Gán Target = Player (hoặc để trống, tự tìm Player)
/// 5. Cấu hình interval, speed, spread, chế độ ngắm
/// 
/// CHẾ ĐỘ NGẮM:
/// - AimMode.FromTurret: bắn chính xác từ vị trí turret về target
/// - AimMode.RandomDirection: bắn theo hướng random quanh target
/// - AimMode.Mixed: kết hợp cả 2 (tỉ lệ %)
/// </summary>
public class ParryTestTurret : MonoBehaviour
{
    public enum AimMode
    {
        FromTurret,        // Bắn từ vị trí turret về phía target
        RandomDirection,   // Bắn theo hướng random quanh turret
        Mixed              // Kết hợp cả 2 theo tỉ lệ
    }

    [Header("=== TARGET ===")]
    [Tooltip("Target để bắn. Để trống → tự tìm Player")]
    [SerializeField] private Transform target;

    [Tooltip("Tự tìm Player nếu target trống")]
    [SerializeField] private bool autoFindPlayer = true;

    [Tooltip("Tag của target (dùng khi autoFindPlayer)")]
    [SerializeField] private string targetTag = "Player";

    [Header("=== PROJECTILE ===")]
    [Tooltip("Prefab đạn (có Rigidbody + ParryTestProjectile)")]
    [SerializeField] private GameObject projectilePrefab;

    [Tooltip("Tốc độ bắn (m/s)")]
    [SerializeField] private float projectileSpeed = 15f;

    [Tooltip("Thời gian sống của đạn (giây) — tự hủy sau đó")]
    [SerializeField] private float projectileLifetime = 5f;

    [Header("=== AUTO FIRE ===")]
    [Tooltip("Bật tự động bắn")]
    [SerializeField] private bool autoFire = true;

    [Tooltip("Tần suất bắn (giây/lần)")]
    [SerializeField] private float fireInterval = 2f;

    [Tooltip("Random ±interval (giây) để đạn không đều")]
    [SerializeField] private float intervalJitter = 0f;

    [Tooltip("Số đạn bắn mỗi lần")]
    [SerializeField] private int projectilesPerShot = 1;

    [Tooltip("Độ lệch giữa các đạn khi bắn nhiều (độ)")]
    [SerializeField] private float multiShotSpread = 15f;

    [Header("=== AIM MODE ===")]
    [SerializeField] private AimMode aimMode = AimMode.FromTurret;

    [Tooltip("Độ tản ngẫu nhiên quanh hướng bắn chính (độ)")]
    [SerializeField] private float spreadAngle = 5f;

    [Tooltip("Chỉ dùng khi AimMode = Mixed. 0 = hoàn toàn FromTurret, 1 = hoàn toàn Random")]
    [Range(0f, 1f)]
    [SerializeField] private float mixedRatio = 0.5f;

    [Header("=== RANDOM DIRECTION (khi AimMode = RandomDirection) ===")]
    [Tooltip("Bán kính random quanh turret (m)")]
    [SerializeField] private float randomRadius = 3f;

    [Tooltip("Chỉ random hướng XZ (bỏ qua Y)")]
    [SerializeField] private bool randomFlat = true;

    [Header("=== DEBUG ===")]
    [SerializeField] private bool debugLog = false;

    [Tooltip("Vẽ hướng bắn trong Scene view")]
    [SerializeField] private bool drawGizmos = true;

    // === RUNTIME ===
    private Transform _cachedTarget;
    private float _nextFireTime;

    private void Start()
    {
        ResolveTarget();
        ScheduleNextFire();
    }

    private void Update()
    {
        // Auto fire
        if (autoFire && Time.time >= _nextFireTime)
        {
            Fire();
            ScheduleNextFire();
        }

        // Refresh target nếu null (Player bị destroy hoặc chưa spawn)
        if (_cachedTarget == null && autoFindPlayer)
        {
            ResolveTarget();
        }
    }

    // =========================================================
    // TARGET RESOLUTION
    // =========================================================
    private void ResolveTarget()
    {
        if (target != null)
        {
            _cachedTarget = target;
            return;
        }

        if (!autoFindPlayer) return;

        var player = GameObject.FindGameObjectWithTag(targetTag);
        if (player != null)
        {
            _cachedTarget = player.transform;
            if (debugLog)
                Debug.Log($"[Turret] Đã tìm thấy target: {player.name}");
        }
    }

    // =========================================================
    // FIRE SCHEDULING
    // =========================================================
    private void ScheduleNextFire()
    {
        float actualInterval = fireInterval + Random.Range(-intervalJitter, intervalJitter);
        _nextFireTime = Time.time + Mathf.Max(0.05f, actualInterval);
    }

    // =========================================================
    // FIRE
    // =========================================================
    [ContextMenu("Fire Now")]
    public void Fire()
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning($"[Turret] {name} chưa gán Projectile Prefab!");
            return;
        }

        if (projectilesPerShot <= 0) return;

        // Xác định hướng bắn chính
        Vector3 baseDirection = CalculateBaseDirection();

        if (debugLog)
            Debug.Log($"[Turret] Bắn {projectilesPerShot} đạn, hướng gốc {baseDirection}");

        // Bắn nhiều đạn
        for (int i = 0; i < projectilesPerShot; i++)
        {
            float multiAngle = 0f;
            if (projectilesPerShot > 1)
            {
                float totalSpread = multiShotSpread * (projectilesPerShot - 1);
                multiAngle = -totalSpread * 0.5f + multiShotSpread * i;
            }

            // Random spread
            float randomAngle = Random.Range(-spreadAngle, spreadAngle);

            Vector3 dir = Quaternion.Euler(0, multiAngle + randomAngle, 0) * baseDirection;
            SpawnProjectile(dir);
        }
    }

    private Vector3 CalculateBaseDirection()
    {
        Vector3 direction;

        switch (aimMode)
        {
            case AimMode.FromTurret:
                direction = GetDirectionToTarget();
                break;

            case AimMode.RandomDirection:
                direction = GetRandomDirection();
                break;

            case AimMode.Mixed:
                direction = Random.value < mixedRatio
                    ? GetRandomDirection()
                    : GetDirectionToTarget();
                break;

            default:
                direction = GetDirectionToTarget();
                break;
        }

        return direction;
    }

    // =========================================================
    // AIM MODE: FromTurret
    // =========================================================
    private Vector3 GetDirectionToTarget()
    {
        if (_cachedTarget != null)
        {
            Vector3 dir = _cachedTarget.position - transform.position;
            dir.y = 0;  // Giữ đạn bay ngang

            if (dir.sqrMagnitude < 0.001f)
                return transform.forward;

            return dir.normalized;
        }

        return transform.forward;
    }

    // =========================================================
    // AIM MODE: RandomDirection
    // =========================================================
    private Vector3 GetRandomDirection()
    {
        if (_cachedTarget == null)
            return Random.onUnitSphere;

        // Random 1 điểm quanh target, rồi bắn về điểm đó
        Vector3 randomOffset;
        if (randomFlat)
        {
            Vector2 rand2D = Random.insideUnitCircle * randomRadius;
            randomOffset = new Vector3(rand2D.x, 0f, rand2D.y);
        }
        else
        {
            randomOffset = Random.insideUnitSphere * randomRadius;
        }

        Vector3 randomPoint = _cachedTarget.position + randomOffset;
        Vector3 dir = randomPoint - transform.position;
        dir.y = 0;

        if (dir.sqrMagnitude < 0.001f)
            return transform.forward;

        return dir.normalized;
    }

    // =========================================================
    // SPAWN PROJECTILE
    // =========================================================
    private void SpawnProjectile(Vector3 direction)
    {
        Vector3 spawnPos = transform.position + direction * 0.5f + Vector3.up * 0.5f;
        Quaternion spawnRot = Quaternion.LookRotation(direction);

        GameObject proj = Instantiate(projectilePrefab, spawnPos, spawnRot);

        // Khởi tạo đạn nếu có ParryTestProjectile
        var projectile = proj.GetComponent<ParryTestProjectile>();
        if (projectile != null)
        {
            projectile.Launch(direction, projectileSpeed);
        }
        else
        {
            // Fallback: set velocity trực tiếp
            var rb = proj.GetComponent<Rigidbody>();
            if (rb != null)
                rb.linearVelocity = direction * projectileSpeed;
        }

        // Tự hủy sau lifetime (phòng trường hợp đạn không có script)
        if (projectileLifetime > 0f)
            Destroy(proj, projectileLifetime);
    }

    // =========================================================
    // GIZMOS
    // =========================================================
    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        // Vẽ vị trí turret
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.3f);

        // Vẽ hướng tới target
        Transform t = target != null ? target : _cachedTarget;
        if (t != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, t.position);
            Gizmos.DrawWireSphere(t.position, 0.5f);

            // Vẽ vùng random quanh target
            if (aimMode == AimMode.RandomDirection || aimMode == AimMode.Mixed)
            {
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
                Gizmos.DrawWireSphere(t.position, randomRadius);
            }
        }
        else
        {
            // Nếu không có target, vẽ forward
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, transform.forward * 3f);
        }
    }
}
