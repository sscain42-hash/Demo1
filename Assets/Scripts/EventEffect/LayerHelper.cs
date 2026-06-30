using UnityEngine;

public static class LayerHelper
{
    /// <summary>
    /// Chuyển đổi từ Tên Layer (String) sang giá trị LayerMask (Int - dạng Bitmask).
    /// Thường dùng trực tiếp cho Physics.Raycast hoặc Physics.OverlapSphere.
    /// </summary>
    /// <example> LayerMask mask = "Enemy".ToLayerMask(); </example>
    public static LayerMask ToLayerMask(this string layerName)
    {
        return 1 << LayerMask.NameToLayer(layerName);
    }

    /// <summary>
    /// Chuyển đổi từ Chỉ số Layer (Int 0-31) sang giá trị LayerMask (Bitmask).
    /// </summary>
    /// <example> LayerMask mask = gameObject.layer.ToLayerMask(); </example>
    public static LayerMask ToLayerMask(this int layerIndex)
    {
        return 1 << layerIndex;
    }

    /// <summary>
    /// Lấy Tên Layer (String) từ một giá trị LayerMask (Chỉ hoạt động chính xác nếu Mask chỉ chứa 1 Layer).
    /// </summary>
    public static string ToLayerName(this LayerMask mask)
    {
        int layerIndex = mask.ToLayerIndex();
        if (layerIndex == -1) return "Invalid Layer";

        return LayerMask.LayerToName(layerIndex);
    }
    /// <summary>
    /// Nhận vào Layer của người bắn, tự động trả về LayerMask của PHE ĐỐI LẬP.
    /// (Nếu là Player/Pet -> Trả về Enemy Mask. Nếu là Enemy -> Trả về Player Mask)
    /// </summary>
    public static LayerMask GetOpponentLayerMask(this int casterLayer)
    {
        string casterLayerName = LayerMask.LayerToName(casterLayer);

        switch (casterLayerName)
        {
            case "Player":
            case "Pet": // Nếu game của bạn có layer riêng cho Pet
                return "Enemy".ToLayerMask();

            case "Enemy":
                // Nếu muốn Enemy bắn trúng được cả Player và Pet của Player
                return "Player".ToLayerMask() | "Pet".ToLayerMask();

            default:
                // Trường hợp mặc định an toàn: Trả về chính nó nếu không thuộc phe nào
                return 1 << casterLayer;
        }
    }
    /// <summary>
    /// Chuyển đổi từ LayerMask (Bitmask) ngược về Chỉ số Layer (Int từ 0-31).
    /// </summary>
    public static int ToLayerIndex(this LayerMask mask)
    {
        int bitmask = mask.value;
        if (bitmask == 0) return -1;

        int index = 0;
        while ((bitmask & 1) == 0)
        {
            bitmask >>= 1;
            index++;
        }
        return index;
    }

    /// <summary>
    /// Kiểm tra xem một GameObject cụ thể có nằm trong cụm LayerMask chỉ định hay không.
    /// </summary>
    /// <example> if(other.gameObject.IsInLayerMask(enemyLayerMask)) { ... } </example>
    public static bool IsInLayerMask(this GameObject obj, LayerMask mask)
    {
        return (mask.value & (1 << obj.layer)) > 0;
    }
}