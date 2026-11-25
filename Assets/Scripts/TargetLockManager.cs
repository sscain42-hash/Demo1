using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Smart Target Lock-on (kiểu kết hợp soft-lock + manual toggle/cycle).
///
/// - AUTO SOFT-LOCK: luôn quét ứng viên trong lockRadius, chọn theo weighted score
///   (gần tâm màn hình + gần player + đang tấn công + trong tầm đánh).
/// - HYSTERESIS: target mới phải thắng target hiện tại đủ retargetMargin mới bị đổi
///   → không bị giật lock qua lại.
/// - TOGGLE: phím toggleLockKey / Middle Mouse → bật/tắt toàn bộ lock-on.
/// - CYCLE: phím cycleTargetKey → nhảy sang ứng viên điểm cao kế tiếp.
/// - Mất lock khi target chết / inactive / vượt loseLockRadius.
/// </summary>
public class TargetLockManager : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════
    // PUBLIC API (giữ tương thích với code cũ)
    // ═══════════════════════════════════════════════════════════
    private GameObject _currentTarget;
    public GameObject CurrentTarget => _currentTarget;
    public bool IsLocked => _currentTarget != null;
    public bool IsSuspended => _userSuspended;

    public event Action<GameObject> OnTargetLocked;
    public event Action OnTargetUnlocked;

    // ═══════════════════════════════════════════════════════════
    // DETECTION
    // ═══════════════════════════════════════════════════════════
    [Header("=== Detection ===")]
    [Tooltip("Layer chứa mục tiêu. Để 0 = tự động dùng layer 'Enemy'")]
    [SerializeField] private LayerMask targetLayer = 0;

    [Tooltip("Bán kính tìm mục tiêu để lock")]
    [SerializeField] private float lockRadius = 15f;

    [Tooltip("Mất lock nếu target xa hơn giá trị này")]
    [SerializeField] private float loseLockRadius = 22f;

    [Tooltip("Khoảng cách coi như 'trong tầm đánh' → cộng điểm ưu tiên")]
    [SerializeField] private float attackRange = 3.5f;

    [Tooltip("Chu kỳ quét lại mục tiêu (giây)")]
    [SerializeField] private float scanInterval = 0.08f;

    // ═══════════════════════════════════════════════════════════
    // SCORING WEIGHTS
    // ═══════════════════════════════════════════════════════════
    [Header("=== Target Scoring ===")]
    [Range(0f, 2f)] [SerializeField] private float weightScreenCenter = 1f;
    [Range(0f, 2f)] [SerializeField] private float weightDistance = 0.6f;
    [Range(0f, 2f)] [SerializeField] private float weightThreat = 0.8f;
    [Range(0f, 2f)] [SerializeField] private float weightInRange = 0.5f;

    [Tooltip("Điểm chênh lệch tối thiểu để tự đổi sang target mới (chống giật lock)")]
    [SerializeField] private float retargetMargin = 0.15f;

    // ═══════════════════════════════════════════════════════════
    // INPUT
    // ═══════════════════════════════════════════════════════════
    [Header("=== Input ===")]
    [Tooltip("Phím bật/tắt lock-on")]
    [SerializeField] private Key toggleLockKey = Key.T;

    [Tooltip("Phím chuyển sang target kế tiếp")]
    [SerializeField] private Key cycleTargetKey = Key.V;

    [Tooltip("Middle Mouse Button cũng dùng để toggle lock-on")]
    [SerializeField] private bool useMiddleMouseToggle = true;

    // ═══════════════════════════════════════════════════════════
    // STATE
    // ═══════════════════════════════════════════════════════════
    [Header("=== State ===")]
    public bool IsLockingEnabled = true;

    private bool _userSuspended;
    private float _scanTimer;
    private Camera _cam;
    private LayerMask _effectiveLayer;

    private readonly Collider[] _hitBuffer = new Collider[32];
    private readonly List<ScoredTarget> _candidates = new List<ScoredTarget>(32);

    private struct ScoredTarget
    {
        public GameObject root;
        public float score;
        public float distance;
    }

    // ═══════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════════════
    private void Awake()
    {
        _effectiveLayer = targetLayer != 0 ? targetLayer : LayerMask.GetMask("Enemy");
        _cam = Camera.main;
    }

    private void Update()
    {
        if (_cam == null) _cam = Camera.main;

        HandleInput();

        _scanTimer -= Time.deltaTime;
        if (_scanTimer > 0f) return;
        _scanTimer = scanInterval;

        UpdateLock();
    }

    // ═══════════════════════════════════════════════════════════
    // INPUT
    // ═══════════════════════════════════════════════════════════
    private void HandleInput()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null && mouse == null) return;

        bool toggle = (kb != null && kb[toggleLockKey].wasPressedThisFrame)
                   || (useMiddleMouseToggle && mouse != null && mouse.middleButton.wasPressedThisFrame);
        bool cycle = kb != null && kb[cycleTargetKey].wasPressedThisFrame;

        if (toggle)
        {
            _userSuspended = !_userSuspended;
            if (_userSuspended)
            {
                Unlock();
            }
            else
            {
                // Bật lại → quét ngay lập tức lấy target tốt nhất
                UpdateLock(true);
            }
        }

        if (cycle && IsLockingEnabled && !_userSuspended)
            CycleTarget();
    }

    // ═══════════════════════════════════════════════════════════
    // CORE LOGIC
    // ═══════════════════════════════════════════════════════════
    private void UpdateLock(bool forceAcquire = false)
    {
        if (!IsLockingEnabled || _userSuspended)
        {
            if (IsLocked) Unlock();
            return;
        }

        // 1. Target hiện tại còn hợp lệ không?
        if (IsLocked && !IsValidTarget(_currentTarget))
        {
            SetTarget(null);
            return;
        }

        // 2. Quét ứng viên
        GatherCandidates();
        if (_candidates.Count == 0)
        {
            // Không còn ai trong tầm → giữ lock nếu chưa quá loseLockRadius (đã check ở trên)
            if (!IsLocked) return;
            // vẫn giữ target cũ, hysteresis sẽ xử lý
            return;
        }

        _candidates.Sort((a, b) => b.score.CompareTo(a.score));
        var best = _candidates[0];

        if (!IsLocked)
        {
            SetTarget(best.root);
            return;
        }

        // 3. Hysteresis — chỉ đổi khi target mới thắng đủ margin
        if (best.root != _currentTarget)
        {
            float currentScore = ScoreOf(_currentTarget);
            if (best.score > currentScore + retargetMargin)
                SetTarget(best.root);
        }
    }

    private void CycleTarget()
    {
        GatherCandidates();
        if (_candidates.Count == 0) return;

        _candidates.Sort((a, b) => b.score.CompareTo(a.score));

        int currentIndex = -1;
        for (int i = 0; i < _candidates.Count; i++)
        {
            if (_candidates[i].root == _currentTarget) { currentIndex = i; break; }
        }

        // Chưa lock hoặc lock target không nằm trong list → lấy best
        int nextIndex = (currentIndex + 1) % _candidates.Count;
        if (nextIndex == currentIndex && _candidates.Count > 1)
            nextIndex = (nextIndex + 1) % _candidates.Count;

        SetTarget(_candidates[nextIndex].root);
    }

    // ═══════════════════════════════════════════════════════════
    // TARGET VALIDITY & SCORING
    // ═══════════════════════════════════════════════════════════
    private bool IsValidTarget(GameObject target)
    {
        if (target == null || !target.activeInHierarchy) return false;

        // Chết rồi (Health về 0) → invalid
        var actor = target.GetComponent<CharacterActor>();
        if (actor != null && actor.Health != null && actor.Health.CurrentValue <= 0)
            return false;

        float dist = Vector3.Distance(transform.position, target.transform.position);
        return dist <= loseLockRadius;
    }

    private void GatherCandidates()
    {
        _candidates.Clear();

        Vector3 origin = transform.position;
        int count = Physics.OverlapSphereNonAlloc(origin, lockRadius, _hitBuffer, _effectiveLayer);

        for (int i = 0; i < count; i++)
        {
            var col = _hitBuffer[i];
            if (col == null) continue;

            // Resolve về root combat object (collider thường nằm trên child)
            var actor = col.GetComponentInParent<CharacterActor>();
            GameObject root = actor != null ? actor.gameObject : col.gameObject;

            // Bỏ qua bản thân player
            if (root == gameObject || root.transform.IsChildOf(transform)) continue;
            if (!root.activeInHierarchy) continue;

            // Actor chết → bỏ
            if (actor != null && actor.Health != null && actor.Health.CurrentValue <= 0)
                continue;

            // Dedupe
            bool dup = false;
            for (int j = 0; j < _candidates.Count; j++)
                if (_candidates[j].root == root) { dup = true; break; }
            if (dup) continue;

            float dist = Vector3.Distance(origin, root.transform.position);
            _candidates.Add(new ScoredTarget
            {
                root = root,
                distance = dist,
                score = ScoreTarget(root, root.transform.position, dist)
            });
        }
    }

    private float ScoreTarget(GameObject root, Vector3 targetPos, float distance)
    {
        float score = 0f;

        // 1. Gần tâm màn hình (camera-forward alignment)
        if (weightScreenCenter > 0f)
        {
            float screenScore = 0f;
            if (_cam != null)
            {
                Vector3 vp = _cam.WorldToViewportPoint(targetPos + Vector3.up * 1f);
                if (vp.z > 0f) // đang ở trước camera
                {
                    float dx = vp.x - 0.5f;
                    float dy = vp.y - 0.5f;
                    float centerDist = Mathf.Sqrt(dx * dx + dy * dy);
                    screenScore = Mathf.Clamp01(1f - centerDist / 0.6f);
                }
            }
            score += weightScreenCenter * screenScore;
        }

        // 2. Khoảng cách — gần hơn điểm cao hơn
        if (weightDistance > 0f)
            score += weightDistance * Mathf.Clamp01(1f - distance / lockRadius);

        // 3. Threat — địch đang trong combo attack được ưu tiên
        if (weightThreat > 0f && IsThreatening(root))
            score += weightThreat;

        // 4. Trong tầm đánh
        if (weightInRange > 0f && distance <= attackRange)
            score += weightInRange;

        return score;
    }

    /// <summary>Địch đang thực hiện combo attack → được ưu tiên lock.</summary>
    private bool IsThreatening(GameObject root)
    {
        var enemyAttack = root.GetComponentInParent<EnemyAttack>();
        return enemyAttack != null && enemyAttack.IsAttacking;
    }

    private float ScoreOf(GameObject target)
    {
        if (target == null) return 0f;
        float dist = Vector3.Distance(transform.position, target.transform.position);
        return ScoreTarget(target, target.transform.position, dist);
    }

    // ═══════════════════════════════════════════════════════════
    // LOCK / UNLOCK
    // ═══════════════════════════════════════════════════════════
    private void SetTarget(GameObject target)
    {
        if (_currentTarget == target) return;

        Unlock();

        _currentTarget = target;
        if (target != null)
        {
            IndicatorManager.Instance?.SpawnIndicator(
                IndicatorKey.LockOnTarget, target.transform, Vector3.zero);
            OnTargetLocked?.Invoke(target);
        }
    }

    public void Unlock()
    {
        if (_currentTarget == null) return;

        _currentTarget = null;
        IndicatorManager.Instance?.ClearActiveIndicator();
        OnTargetUnlocked?.Invoke();
    }

    /// <summary>Giữ tương thích: gọi từ Detection event cũ (nếu còn dùng).</summary>
    public void TryLock(GameObject target)
    {
        if (!IsLockingEnabled || _userSuspended || IsLocked || target == null) return;
        if (!IsValidTarget(target)) return;
        SetTarget(target);
    }

    // ═══════════════════════════════════════════════════════════
    // GIZMOS
    // ═══════════════════════════════════════════════════════════
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, lockRadius);
        Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, loseLockRadius);
    }
#endif
}
