using UnityEngine;

public interface IIndicatorBehaviour
{
    // Hàm khởi tạo nạp dữ liệu (Dành cho Indicator bám mục tiêu hoặc định hướng)
    void Initialize(Transform caster, Transform target, Vector3 offset, Vector3 scale);

    // Hàm cập nhật chạy mỗi Frame (Nếu loại indicator đó cần bám theo mục tiêu/chuột)
    void Tick();
}