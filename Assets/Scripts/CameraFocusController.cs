using UnityEngine;
using Unity.Cinemachine;

public class CameraFocusController : MonoBehaviour
{
    [SerializeField] private CinemachineTargetGroup _targetGroup;
    [SerializeField] private Transform _playerTransform;
    [SerializeField] private float _enemyWeight = 1f;
    [SerializeField] private float _enemyRadius = 5f;

    // Đăng ký vào Event của TargetLockManager
    public void Setup(TargetLockManager lockManager)
    {
        lockManager.OnTargetLocked += FocusOnTarget;
        lockManager.OnTargetUnlocked += ResetFocus;
    }

    private void FocusOnTarget(GameObject target)
    {
        // Thêm mục tiêu vào nhóm Camera
        _targetGroup.AddMember(target.transform, _enemyWeight, _enemyRadius);
        Debug.Log($"Camera now focusing on: {target.name}");
    }

    private void ResetFocus()
    {
        // Loại bỏ mục tiêu cũ khỏi nhóm
        // Lưu ý: Nếu bạn có nhiều target, cần loop để tìm đúng member cần xóa
        // Cách nhanh nhất là xóa tất cả member không phải là Player
        for (int i = 0; i < _targetGroup.Targets.Count; i++)
        {
            if (_targetGroup.Targets[i].Object != _playerTransform)
            {
                _targetGroup.RemoveMember(_targetGroup.Targets[i].Object);
            }
        }
    }
    public void AddEnemyToFocus(Transform enemyTransform)
    {
        // Thêm Enemy vào nhóm, Camera sẽ tự động cân bằng lại
        _targetGroup.AddMember(enemyTransform, 1f, 5f);
    }

    public void RemoveEnemyFromFocus(Transform enemyTransform)
    {
        _targetGroup.RemoveMember(enemyTransform);
    }
}
