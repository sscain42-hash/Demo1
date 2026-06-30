// Một lớp "cái túi" chứa thông tin sự kiện
using UnityEngine;
using UnityEngine.AI;

public static class PositionHelper
{
    /// <summary>
    /// Lấy một vị trí ngẫu nhiên trên mặt phẳng nằm ngang (X Z) xung quanh một tâm chỉ định.
    /// (Thường dùng cho Game 3D, di chuyển quái, AI)
    /// </summary>
    /// <param name="center">Vị trí tâm</param>
    /// <param name="radius">Bán kính tối đa</param>
    /// <param name="minRadius">Bán kính tối thiểu (tránh trùng khít tâm)</param>
    public static Vector3 GetRandomPositionXZ(this Vector3 center, float radius, float minRadius = 0f)
    {
        // Lấy một hướng ngẫu nhiên trên vòng tròn 2D
        Vector2 randomCircle = Random.insideUnitCircle.normalized;

        // Lấy khoảng cách ngẫu nhiên từ minRadius đến radius
        float randomDistance = Random.Range(minRadius, radius);

        // Chuyển đổi sang không gian 3D (X, Z) và cộng vào tâm
        Vector3 randomPos = new Vector3(randomCircle.x, 0f, randomCircle.y) * randomDistance;
        return center + randomPos;
    }

    /// <summary>
    /// Lấy một vị trí ngẫu nhiên xung quanh tâm và ĐẢM BẢO vị trí đó nằm trên lưới NavMesh.
    /// (Bắt buộc phải dùng bản này nếu bạn muốn gán thẳng vị trí cho NavMeshAgent của Pet/Enemy)
    /// </summary>
    public static Vector3 GetRandomNavMeshPosition(this Vector3 center, float radius, float minRadius = 0f)
    {
        Vector3 targetPos = center.GetRandomPositionXZ(radius, minRadius);
        NavMeshHit hit;

        // Tìm điểm gần nhất trên NavMesh trong phạm vi bán kính cho phép (ví dụ tìm trong khoảng 2.0f xung quanh điểm vừa random)
        if (NavMesh.SamplePosition(targetPos, out hit, radius, NavMesh.AllAreas))
        {
            return hit.position;
        }

        // Nếu đen đủi không tìm thấy điểm NavMesh nào (vị trí rơi ra ngoài map), trả về tâm ban đầu làm điểm an toàn
        return center;
    }

    /// <summary>
    /// Lấy một vị trí ngẫu nhiên dạng hình cầu 3D xung quanh tâm (Dùng cho game bay lượn, bơi lội, không gian)
    /// </summary>
    public static Vector3 GetRandomPosition3D(this Vector3 center, float radius, float minRadius = 0f)
    {
        Vector3 randomDirection = Random.onUnitSphere;
        float randomDistance = Random.Range(minRadius, radius);
        return center + (randomDirection * randomDistance);
    }
}