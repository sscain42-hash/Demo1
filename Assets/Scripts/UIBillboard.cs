using UnityEngine;

public class UILockOnBillboard : MonoBehaviour
{
    private Transform _mainCameraTransform;

    private void Start()
    {
        if (Camera.main != null)
        {
            _mainCameraTransform = Camera.main.transform;
        }
    }

    private void LateUpdate()
    {
        if (_mainCameraTransform == null) return;

        // Tính toán hướng từ tâm UI đến Camera
        Vector3 targetDirection = _mainCameraTransform.position - transform.position;

        // Nếu bạn chơi game góc nhìn Top-down hoặc muốn hồng tâm luôn đứng thẳng vuông góc với mặt đất:
        targetDirection.y = 0;

        if (targetDirection != Vector3.zero)
        {
            // Ép quay mặt về phía Camera
            transform.rotation = Quaternion.LookRotation(-targetDirection);
        }
    }
}