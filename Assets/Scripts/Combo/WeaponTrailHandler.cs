using UnityEngine;

public class WeaponTrailHandler : MonoBehaviour
{
    [SerializeField] private TrailRenderer swordTrail; // Hoặc kiểu dữ liệu của Asset Trail bạn dùng

    // Gọi hàm này từ Animation Event ở thời điểm bắt đầu chém
    public void EnableTrail()
    {
        if (swordTrail != null)
        {
            swordTrail.Clear(); // Xóa vết cũ nếu có
            swordTrail.enabled = true; // Hoặc swordTrail.gameObject.SetActive(true);
        }
    }

    // Gọi hàm này từ Animation Event ở thời điểm thu kiếm / kết thúc đòn chém
    public void DisableTrail()
    {
        if (swordTrail != null)
        {
            swordTrail.enabled = false; // Hoặc swordTrail.gameObject.SetActive(false);
        }
    }
}

