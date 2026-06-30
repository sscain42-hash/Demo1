using System.Collections.Generic;
using UnityEngine;

public class CharacterEffect : MonoBehaviour
{
    private IDamageProvider Sender;

    // 📦 POOL STORAGE
    private static Dictionary<GameObject, ObjectPooler<Reference>> _globalDynamicPools = new Dictionary<GameObject, ObjectPooler<Reference>>();
    private static Transform _projectilePoolAnchor;

    // ⚡ CHÌA KHÓA HIỆU NĂNG (CACHING SYSTEM)
    private static Dictionary<Reference, DetectionBase> _componentCache = new Dictionary<Reference, DetectionBase>();
    // 🔥 MỚI: Cache lại Transform của các Anchor để không bao giờ phải gánh GameObject.Find() nữa
    private static Dictionary<GameObject, Transform> _anchorCache = new Dictionary<GameObject, Transform>();

    private void Awake()
    {
        Sender = GetComponent<IDamageProvider>();
        InitializeWorldAnchor();
    }

    private void InitializeWorldAnchor()
    {
        if (_projectilePoolAnchor == null)
        {
            GameObject anchor = GameObject.Find("[DYNAMIC_PROJECTILE_POOL]");
            if (anchor == null) anchor = new GameObject("[DYNAMIC_PROJECTILE_POOL]");
            _projectilePoolAnchor = anchor.transform;
        }
    }

    public Reference SpawnVFXFromData(Reference prefabFromData, Vector3 position, Quaternion rotation, AttackType type)
    {
        if (prefabFromData == null) return null;

        GameObject prefabKey = prefabFromData.gameObject;

        // 1. KHỞI TẠO VÀ CACHE ANCHOR / POOL (Chỉ chạy DUY NHẤT lần đầu tiên gọi Prefab này)
        if (!_globalDynamicPools.ContainsKey(prefabKey))
        {
            int defaultSize = 15;

            // Kiểm tra xem Anchor của riêng Prefab này đã được tìm và lưu lại trước đó chưa
            if (!_anchorCache.TryGetValue(prefabKey, out Transform specificAnchor))
            {
                GameObject anchorGO = GameObject.Find($"[POOL_{prefabKey.name}]");
                if (anchorGO == null)
                {
                    anchorGO = new GameObject($"[POOL_{prefabKey.name}]");
                    anchorGO.transform.SetParent(_projectilePoolAnchor);
                }
                specificAnchor = anchorGO.transform;

                // Lưu lại vào bộ nhớ đệm
                _anchorCache.Add(prefabKey, specificAnchor);
            }

            var newPool = new ObjectPooler<Reference>(prefabFromData, specificAnchor, defaultSize);
            _globalDynamicPools.Add(prefabKey, newPool);
        }

        // 2. LẤY ĐẠN TỪ POOL RA (Tốc độ tối đa O(1))
        Reference spawnedInstance = _globalDynamicPools[prefabKey].Get(position, rotation);

        if (spawnedInstance != null)
        {
            // 3. TRUY XUẤT COMPONENT TỪ CACHE (Bỏ qua GetComponent)
            if (!_componentCache.TryGetValue(spawnedInstance, out DetectionBase vfx))
            {
                vfx = spawnedInstance.GetComponent<DetectionBase>();
                if (vfx != null)
                {
                    _componentCache.Add(spawnedInstance, vfx);
                }
            }

            // 4. NẠP ĐỒNG BỘ EVENT VÀ CHẠY NGAY LẬP TỨC
            if (vfx != null)
            {
                vfx.CollisionEnterEvent.RemoveAllListeners();
                vfx.CollisionEnterEvent.AddListener((victim) => HandleHit(victim, type));
            }
        }

        return spawnedInstance;
    }

    private void HandleHit(GameObject victim, AttackType type)
    {
        if (victim == null) return;

        // Bộ lọc phân biệt phe phái (Team Filter)
        if (gameObject.CompareTag("Player") && !victim.CompareTag("Enemy")) return;
        if (gameObject.CompareTag("Enemy") && !victim.CompareTag("Player")) return;

        if (Sender != null)
        {
            Sender.ExecuteDamage(victim, type);
        }
       
    }
}