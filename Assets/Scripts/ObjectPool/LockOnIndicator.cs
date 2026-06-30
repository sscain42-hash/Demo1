using UnityEngine;

public class LockOnIndicator : MonoBehaviour, IIndicatorBehaviour
{
    private Transform _target;
    private Vector3 _offset;
    private bool _isTracking;

    public void Initialize(Transform caster, Transform target, Vector3 offset,Vector3 scale)
    {
        _target = target;
        _offset = offset;
        _isTracking = _target != null;
       
        // Điều chỉnh kích thước của Canvas trong không gian 3D dựa theo đòn đánh
        transform.localScale = target.localScale;
    }

    public void Tick()
    {
        // Nếu mục tiêu chết hoặc biến mất -> Tự động trả bản thân về Pool
        if (!_isTracking || _target == null || !_target.gameObject.activeInHierarchy)
        {
            if (TryGetComponent(out Reference myRef))
            {
                myRef.Release();
            }
            return;
        }

        // 🔥 BÁM CHẶT: Tính toán vị trí ở LateUpdate để không bao giờ bị chậm nhịp
        transform.position = _target.position + _offset;
    }
}