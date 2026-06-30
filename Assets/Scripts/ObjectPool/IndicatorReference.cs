using UnityEngine;

public class IndicatorReference : Reference
{
    private IIndicatorBehaviour _behaviour;

    private void Awake()
    {
        // Tự động tìm bộ não cụ thể (Lock-on, AOE...) được gắn trên cùng Prefab
        _behaviour = GetComponent<IIndicatorBehaviour>();
    }

    public void Activate(Transform caster, Transform target, Vector3 offset, Vector3 scale)
    {
        _behaviour?.Initialize(caster, target, offset, scale);
    }

    private void Update()
    {
        // Chạy logic cập nhật của riêng loại indicator đó
        _behaviour?.Tick();
    }
}