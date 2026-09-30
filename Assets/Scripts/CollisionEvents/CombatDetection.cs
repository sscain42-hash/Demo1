using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CombatDetection : PhysicsDetection
{
    [Header("Combat Detection Extra Settings")]
    [Tooltip("Số lượng mục tiêu tối đa có thể lưu trong bộ nhớ đệm mỗi lần quét")]
    [SerializeField] private int maxBufferCount = 15;

    [Tooltip("Góc hình quạt phía trước để lọc mục tiêu (360 = xung quanh, 120 = góc nhìn trước mặt)")]
    [Range(0f, 360f)]
    [SerializeField] private float combatAngle = 360f;

    [Tooltip("Bắt buộc mục tiêu phải có component kế thừa từ Damageable")]
    [SerializeField] private bool requireDamageableComponent = false;

    private Collider[] _combatHitColliders;

    private void Awake()
    {
        _combatHitColliders = new Collider[maxBufferCount];
    }

    #region COMBAT QUERIES (HÀM CUNG CẤP THÔNG TIN CHIẾN ĐẤU)

    /// <summary>
    /// Kiểm tra xem có bất kỳ mục tiêu hợp lệ nào trong vùng quét không.
    /// </summary>
    public bool HasTarget(GameObject selfToIgnore = null)
    {
        return GetNearestTarget(transform.position, transform.forward, selfToIgnore) != null;
    }

    /// <summary>
    /// Lấy danh sách tất cả các mục tiêu hợp lệ trong vùng quét (đã lọc theo Layer, Góc nhìn, Damageable).
    /// </summary>
    public List<GameObject> GetAllTargets(Vector3 origin, Vector3 forward, GameObject selfToIgnore = null)
    {
        List<GameObject> result = new List<GameObject>();
        int count = OverlapSphereNonAlloc();

        for (int i = 0; i < count; i++)
        {
            Collider col = _combatHitColliders[i];
            if (!IsValidTarget(col, origin, forward, selfToIgnore))
                continue;

            result.Add(col.gameObject);
        }

        return result;
    }

    /// <summary>
    /// Tìm mục tiêu gần nhất thỏa mãn các điều kiện chiến đấu.
    /// </summary>
    public GameObject GetNearestTarget(Vector3 origin, Vector3 forward, GameObject selfToIgnore = null)
    {
        int count = OverlapSphereNonAlloc();
        GameObject nearest = null;
        float minDistanceSqr = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Collider col = _combatHitColliders[i];
            if (!IsValidTarget(col, origin, forward, selfToIgnore))
                continue;

            float distSqr = (col.transform.position - origin).sqrMagnitude;
            if (distSqr < minDistanceSqr)
            {
                minDistanceSqr = distSqr;
                nearest = col.gameObject;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Tính Vector3 hướng đã chuẩn hóa (Normalized) từ Origin tới mục tiêu gần nhất.
    /// Phục vụ cho Lunge, Auto-Lock, Soft-Lock và xoay nhân vật khi đánh.
    /// </summary>
    public Vector3 GetNearestTargetDirection(Vector3 origin, Vector3 fallbackForward, GameObject selfToIgnore = null, bool flattenY = true)
    {
        GameObject nearest = GetNearestTarget(origin, fallbackForward, selfToIgnore);
        if (nearest == null)
        {
            Vector3 fallback = fallbackForward;
            if (flattenY) fallback.y = 0f;
            return fallback.normalized;
        }

        Vector3 dir = nearest.transform.position - origin;
        if (flattenY) dir.y = 0f;

        return dir.sqrMagnitude > 0.001f ? dir.normalized : fallbackForward.normalized;
    }

    /// <summary>
    /// Lấy danh sách tất cả các mục tiêu và sắp xếp theo thứ tự từ GẦN NHẤT đến XA NHẤT.
    /// Phục vụ cho đòn đánh chuỗi (Chain Attack) hoặc nảy tia (Ricochet).
    /// </summary>
    public List<GameObject> GetTargetsSortedByDistance(Vector3 origin, Vector3 forward, GameObject selfToIgnore = null)
    {
        var targets = GetAllTargets(origin, forward, selfToIgnore);
        return targets.OrderBy(t => (t.transform.position - origin).sqrMagnitude).ToList();
    }

    /// <summary>
    /// Lấy khoảng cách từ Origin tới mục tiêu gần nhất (trả về float.MaxValue nếu không tìm thấy).
    /// </summary>
    public float GetDistanceToNearestTarget(Vector3 origin, Vector3 forward, GameObject selfToIgnore = null)
    {
        GameObject nearest = GetNearestTarget(origin, forward, selfToIgnore);
        if (nearest == null) return float.MaxValue;

        return Vector3.Distance(origin, nearest.transform.position);
    }

    #endregion

    #region INTERNAL HELPER METHODS

    private int OverlapSphereNonAlloc()
    {
        if (_combatHitColliders == null || _combatHitColliders.Length != maxBufferCount)
        {
            _combatHitColliders = new Collider[maxBufferCount];
        }

        return Physics.OverlapSphereNonAlloc(transform.position, radiusCheck, _combatHitColliders, layerToCheck);
    }

    private bool IsValidTarget(Collider col, Vector3 origin, Vector3 forward, GameObject selfToIgnore)
    {
        if (col == null) return false;

        GameObject obj = col.gameObject;

        // 1. Bỏ qua bản thân hoặc con của bản thân
        if (selfToIgnore != null && (obj == selfToIgnore || obj.transform.IsChildOf(selfToIgnore.transform)))
            return false;

        // 2. Lọc theo góc nhìn (Combat Angle / FOV)
        if (combatAngle < 360f)
        {
            Vector3 dirToTarget = (col.transform.position - origin);
            dirToTarget.y = 0f;
            Vector3 checkForward = forward;
            checkForward.y = 0f;

            if (dirToTarget != Vector3.zero && checkForward != Vector3.zero)
            {
                float angle = Vector3.Angle(checkForward, dirToTarget);
                if (angle > combatAngle * 0.5f)
                    return false;
            }
        }

        // 3. Lọc theo Component Damageable
        if (requireDamageableComponent)
        {
            if (!obj.TryGetComponent<Damageable>(out _))
                return false;
        }

        return true;
    }

    #endregion

    #region EDITOR GIZMOS

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (combatAngle < 360f && combatAngle > 0f)
        {
            Gizmos.color = Color.yellow;
            Vector3 fwd = transform.forward;
            Vector3 leftRay = Quaternion.Euler(0, -combatAngle * 0.5f, 0) * fwd;
            Vector3 rightRay = Quaternion.Euler(0, combatAngle * 0.5f, 0) * fwd;

            Gizmos.DrawRay(transform.position, leftRay * radiusCheck);
            Gizmos.DrawRay(transform.position, rightRay * radiusCheck);
        }
    }
#endif

    #endregion
}