
using UnityEngine;

public static class RotationHelper
{
    /// <summary>
    /// Xoay ngẫu nhiên hoàn toàn trên cả 3 trục (X, Y, Z).
    /// Dùng cho: Mảnh vỡ nổ (Debris), thiên thạch, xúc xắc.
    /// </summary>
    public static Quaternion RandomFull3D()
    {
        // rotationUniform phân phối xác suất đều hơn Random.rotation
        return Random.rotationUniform;
    }

    /// <summary>
    /// Chỉ xoay ngẫu nhiên trên trục Y (0 đến 360 độ).
    /// Dùng cho: Spawn quái vật, trồng cây, NPC đứng trên mặt đất.
    /// </summary>
    public static Quaternion RandomY()
    {
        float randomY = Random.Range(0f, 360f);
        return Quaternion.Euler(0f, randomY, 0f);
    }

    /// <summary>
    /// Xoay ngẫu nhiên trong một khoảng góc nhất định cho từng trục.
    /// Dùng cho: Độ giật của súng (Recoil), camera rung lắc (Camera Shake).
    /// </summary>
    public static Quaternion RandomInRange(Vector3 minAngles, Vector3 maxAngles)
    {
        float x = Random.Range(minAngles.x, maxAngles.x);
        float y = Random.Range(minAngles.y, maxAngles.y);
        float z = Random.Range(minAngles.z, maxAngles.z);
        return Quaternion.Euler(x, y, z);
    }

    /// <summary>
    /// Tạo một góc lệch ngẫu nhiên so với hướng ban đầu (Hình nón).
    /// Dùng cho: Độ tản mát của đạn (Bullet Spread), tia lửa văng ra.
    /// </summary>
    /// <param name="originalRotation">Góc xoay ban đầu</param>
    /// <param name="maxSpreadAngle">Độ lệch tối đa (tính bằng độ)</param>
    public static Quaternion RandomSpread(Quaternion originalRotation, float maxSpreadAngle)
    {
        float spreadX = Random.Range(-maxSpreadAngle, maxSpreadAngle);
        float spreadY = Random.Range(-maxSpreadAngle, maxSpreadAngle);

        // Nhân Quaternion để cộng dồn góc xoay
        Quaternion spread = Quaternion.Euler(spreadX, spreadY, 0f);
        return originalRotation * spread;
    }
    public static Quaternion RandomSlashRotation(Vector3 hitDirection)
    {
        // 1. Định hướng cho hiệu ứng quay về phía cú đánh
        Quaternion lookRotation = Quaternion.LookRotation(hitDirection);

        // 2. Tạo một góc xoay ngẫu nhiên quanh trục dọc của chính vết chém (0 - 360 độ)
        float randomZ = Random.Range(0f, 360f);
        Quaternion randomRoll = Quaternion.Euler(0f, 0f, randomZ);

        // 3. Kết hợp cả 2 (Phép nhân Quaternion)
        return lookRotation * randomRoll;
    }
}