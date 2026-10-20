using UnityEngine;

public interface IParryReceiver
{
    /// <summary>
    /// Trả về true nếu đòn đánh bị Parry.
    /// </summary>
    /// <param name="attacker">Kẻ tấn công</param>
    /// <param name="hitPoint">Điểm va chạm thực tế (world space) — dùng để spawn VFX</param>
    bool TryParry(GameObject attacker, Vector3 hitPoint);
}
