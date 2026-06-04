using UnityEngine;

public class ProjectileMover
{
    private readonly Transform _transform;
    private float _speed;
    private bool _isFlying;

    // Hàm khởi tạo (Constructor) nhận vào Transform của GameObject để di chuyển nó
    public ProjectileMover(Transform transform)
    {
        _transform = transform;
        _isFlying = false;
    }

    // Hàm nạp dữ liệu cấu hình bay từ Controller
    public void SetMovement(Vector3 direction, float speed)
    {
        _speed = speed;
        _isFlying = true;

        // Nếu direction là Vector3.zero, đạn sẽ không có hướng để bay
        if (direction != Vector3.zero && _transform != null)
        {
            _transform.forward = direction;
        }

        Debug.Log($"Đạn đã được set speed: {speed}, direction: {direction}");
    }

    // Hàm cập nhật vị trí bay (sẽ được Controller gọi trong Update)
    public void Tick(float deltaTime)
    {
        // Nếu _isFlying vẫn là false, đạn sẽ không bao giờ bay
        if (!_isFlying || _transform == null) return;

        // Kiểm tra xem có đang thực sự translate không
        _transform.Translate(Vector3.forward * _speed * deltaTime);
    }

    public void Stop()
    {
        _isFlying = false;
    }
}