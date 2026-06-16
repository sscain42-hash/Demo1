using UnityEngine;

public class ProjectileController : MonoBehaviour
{
    [Header("Settings")]
  

    private ProjectileMover _mover;
    private float _lifeTimer;
    private bool _isActive;

    private void Awake()
    {
        // Khởi tạo bộ di chuyển
        _mover = new ProjectileMover(transform);
    }

    private void Update()
    {
        if (!_isActive) return;

        // Cập nhật di chuyển
        _mover?.Tick(Time.deltaTime);

        // Đếm ngược thời gian sống
        _lifeTimer -= Time.deltaTime;
        if (_lifeTimer <= 0f)
        {
            ResetProjectile();
        }
    }

    public void Initialize(Vector3 direction, float speed, float lifeTime)
    {
        _lifeTimer = lifeTime;
        _isActive = true;

        if (_mover != null)
        {
            _mover.SetMovement(direction, speed);
        }
        else
        {
            Debug.LogError($"{gameObject.name}: ProjectileMover chưa được khởi tạo!");
        }
    }

    public void OnDetectionHitPosition(Vector3 hitPosition)
    {
        if (!_isActive) return;

        _isActive = false;
        _mover?.Stop();

     

        ResetProjectile();
    }

    private void ResetProjectile()
    {
        _isActive = false;
        _mover?.Stop();

        // Trả viên đạn về Pool nếu có script Reference
        if (TryGetComponent(out Reference bulletRef))
        {
            bulletRef.Release();
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}
