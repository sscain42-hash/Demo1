using UnityEngine;

/// <summary>
/// Interface cho bất kỳ GameObject nào có thể parry đòn đánh.
/// Enemy sẽ gọi TryParry() trước khi trừ máu.
/// </summary>
public interface IParryReceiver
{
    /// <summary>
    /// Trả về true nếu đòn đánh bị Parry (không gây sát thương).
    /// </summary>
    /// <param name="attacker">Kẻ tấn công (để xoay nhân vật về phía đó)</param>
    /// <returns>True = đã parry, False = không parry được</returns>
    bool TryParry(GameObject attacker);
}