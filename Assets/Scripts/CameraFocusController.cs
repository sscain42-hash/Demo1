using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Điều khiển CinemachineTargetGroup theo TargetLockManager.
/// Khi lock: thêm target vào group → camera tự reframe ôm cả player + địch.
/// Khi unlock/đổi target: dọn mọi member không phải player rồi add target mới.
/// Ưu tiên bám vào child "HitPoint" (ngực) nếu có để framing đẹp hơn.
/// </summary>
public class CameraFocusController : MonoBehaviour
{
    [SerializeField] private CinemachineTargetGroup _targetGroup;
    [SerializeField] private Transform _playerTransform;
    [SerializeField] private float _enemyWeight = 1f;
    [SerializeField] private float _enemyRadius = 5f;

    [Header("=== Mode ===")]
    [Tooltip("BẬT: camera focus/khung cả player + target khi lock-on. " +
             "TẮT (mặc định): camera tự do xoay quanh player — phù hợp đánh boss lớn.")]
    [SerializeField] private bool focusCameraOnLock = false;

    private void Awake()
    {
        // Tự tìm player nếu chưa gán — hoặc nếu gán nhầm CAMERA
        // (camera nằm trong TargetGroup sẽ tạo feedback loop → camera rung loạn)
        if (_playerTransform == null || IsUnderCamera(_playerTransform))
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null)
                _playerTransform = pc.transform;
            else if (IsUnderCamera(_playerTransform))
            {
                Debug.LogWarning("[CameraFocusController] Player Transform đang trỏ vào CAMERA — " +
                                 "hãy gán transform của PLAYER, không phải camera!");
                _playerTransform = null;
            }
        }

        if (_targetGroup == null)
            Debug.LogWarning("[CameraFocusController] Chưa gán CinemachineTargetGroup!");

        if (!focusCameraOnLock) return;

        // Đảm bảo player luôn nằm trong group
        EnsurePlayerMember();
    }

    private static bool IsUnderCamera(Transform t)
    {
        if (t == null) return false;
        return t.GetComponentInParent<Camera>(true) != null
            || t.GetComponentInChildren<Camera>(true) != null
            || t.GetComponentInParent<CinemachineVirtualCamera>(true) != null
            || t.GetComponentInChildren<CinemachineVirtualCamera>(true) != null;
    }

    // Đăng ký vào Event của TargetLockManager
    public void Setup(TargetLockManager lockManager)
    {
        if (lockManager == null) return;

        // Chống subscribe trùng
        lockManager.OnTargetLocked -= FocusOnTarget;
        lockManager.OnTargetUnlocked -= ResetFocus;
        lockManager.OnTargetLocked += FocusOnTarget;
        lockManager.OnTargetUnlocked += ResetFocus;
    }

    private void FocusOnTarget(GameObject target)
    {
        if (!focusCameraOnLock) return;
        if (_targetGroup == null || target == null) return;

        Transform focusPoint = ResolveFocusPoint(target.transform);

        // Tránh add trùng member
        if (IsMember(focusPoint)) return;

        _targetGroup.AddMember(focusPoint, _enemyWeight, _enemyRadius);
    }

    private void ResetFocus()
    {
        if (!focusCameraOnLock) return;
        if (_targetGroup == null) return;

        // Duyệt NGƯỢC để xóa không bị bỏ sót phần tử
        for (int i = _targetGroup.Targets.Count - 1; i >= 0; i--)
        {
            var member = _targetGroup.Targets[i].Object;
            if (member != _playerTransform && member != null)
                _targetGroup.RemoveMember(member);
        }
    }

    public void AddEnemyToFocus(Transform enemyTransform)
    {
        if (_targetGroup == null || enemyTransform == null) return;
        Transform fp = ResolveFocusPoint(enemyTransform);
        if (!IsMember(fp))
            _targetGroup.AddMember(fp, _enemyWeight, _enemyRadius);
    }

    public void RemoveEnemyFromFocus(Transform enemyTransform)
    {
        if (_targetGroup == null || enemyTransform == null) return;
        Transform fp = ResolveFocusPoint(enemyTransform);
        if (IsMember(fp))
            _targetGroup.RemoveMember(fp);
    }

    // ═══════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════
    private void EnsurePlayerMember()
    {
        if (_targetGroup == null || _playerTransform == null) return;
        if (!IsMember(_playerTransform))
            _targetGroup.AddMember(_playerTransform, 1f, _enemyRadius);
    }

    private bool IsMember(Transform t)
    {
        if (t == null || _targetGroup == null) return false;
        foreach (var m in _targetGroup.Targets)
            if (m.Object == t) return true;
        return false;
    }

    /// <summary>Tìm điểm focus tốt nhất: child "HitPoint" → fallback chính transform.</summary>
    private Transform ResolveFocusPoint(Transform target)
    {
        Transform hitPoint = FindChildDeep(target, "HitPoint");
        return hitPoint != null ? hitPoint : target;
    }

    private Transform FindChildDeep(Transform parent, string childName)
    {
        if (parent.name == childName) return parent;
        foreach (Transform child in parent)
        {
            Transform result = FindChildDeep(child, childName);
            if (result != null) return result;
        }
        return null;
    }
}
