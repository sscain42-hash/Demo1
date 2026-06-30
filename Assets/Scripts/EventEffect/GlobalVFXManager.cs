// Một lớp "cái túi" chứa thông tin sự kiện
using System.Collections.Generic;
using UnityEngine;

public static class GlobalVFXManager
{
    private static Dictionary<GameObject, ObjectPooler<Reference>> _vfxPools = new Dictionary<GameObject, ObjectPooler<Reference>>();
    private static Transform _poolRoot;

    /// <summary>
    /// Hàm tĩnh vạn năng để rút VFX vô chủ từ Pool toàn cục
    /// </summary>
    public static Reference SpawnGlobalVFX(Reference prefab, Vector3 position, Quaternion rotation, Vector3? scale = null)
    {
        if (prefab == null) return null;

        GameObject prefabKey = prefab.gameObject;

        // Nếu Prefab này lần đầu được gọi trong map, tự động tạo cụm Pool riêng cho nó
        if (!_vfxPools.ContainsKey(prefabKey))
        {
            InitializePoolForPrefab(prefab, prefabKey);
        }

        // Rút nhanh VFX ra từ Pool bằng Key O(1)
        Reference vfxInstance = _vfxPools[prefabKey].Get(position, rotation);

        if (vfxInstance != null)
        {
            // Cập nhật lại Scale nếu Designer có yêu cầu tùy biến kích thước đòn đánh
            if (scale.HasValue)
            {
                vfxInstance.transform.localScale = scale.Value;
            }
        }

        // Trả về Instance. Khi Particle chạy xong, script nội tại của bạn sẽ tự động 
        // đưa nó về trạng thái Deactive (SetActive(false)), sẵn sàng cho lần gọi kế tiếp.
        return vfxInstance;
    }

    private static void InitializePoolForPrefab(Reference prefab, GameObject prefabKey)
    {
        // Tạo một Empty GameObject chung để gom nhóm toàn bộ Pool trong Scene cho gọn Hierarchy
        if (_poolRoot == null)
        {
            _poolRoot = new GameObject("[GLOBAL_VFX_SERVICE_POOL]").transform;
            Object.DontDestroyOnLoad(_poolRoot.gameObject);
        }

        // Tạo Container riêng cho từng loại Prefab cụ thể để dễ quản lý debug
        GameObject specificAnchor = new GameObject($"[POOL_{prefabKey.name}]");
        specificAnchor.transform.SetParent(_poolRoot);

        // Khởi tạo ObjectPooler gốc của bạn với kích thước đệm ban đầu là 10
        var newPool = new ObjectPooler<Reference>(prefab, specificAnchor.transform, 10);
        _vfxPools.Add(prefabKey, newPool);
    }
}
