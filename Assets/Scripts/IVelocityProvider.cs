using UnityEngine;

public interface IVelocityProvider
{
    Vector3 GetVelocityModifier(); // Lực muốn tác động
    bool IsActive { get; }         // Có đang hoạt động không?
    int Priority { get; }          // Độ ưu tiên (nếu nhiều lực cùng tác động)
}