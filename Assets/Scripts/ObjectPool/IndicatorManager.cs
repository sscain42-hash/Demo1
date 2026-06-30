using System.Collections.Generic;
using UnityEngine;

// Định nghĩa Enum để dễ dàng lựa chọn trong Inspector
public enum IndicatorKey
{
    None,
    LockOnTarget,
    SkillshotArrow,
    CircleAOE,
    ConeSector
}

public class IndicatorManager : Singleton<IndicatorManager>
{
    [System.Serializable]
    public struct IndicatorMapping
    {
        public IndicatorKey key;
        public Reference prefab;
    }

    [Header("🗂️ INDICATOR DATABASE")]
    [SerializeField] private List<IndicatorMapping> indicatorDatabase = new List<IndicatorMapping>();

    private Dictionary<GameObject, ObjectPooler<Reference>> _indicatorPools = new();
    private Reference _activeIndicator;
    private GameObject _currentPrefabKey;

    protected override void Awake()
    {
        base.Awake();
    }

    public void SpawnIndicator(IndicatorKey key, Transform target, Vector3 offset)
    {
        if (key == IndicatorKey.None)
        {
            ClearActiveIndicator();
            return;
        }

        Reference foundPrefab = GetPrefabFromDatabase(key);
        if (foundPrefab == null)
        {
            Debug.LogWarning($"[Indicator Error] Không tìm thấy Prefab nào được gán cho Key: {key}!");
            return;
        }

        SpawnIndicator(foundPrefab, target, offset);
    }

    public void SpawnIndicator(Reference prefab, Transform target, Vector3 offset)
    {
        if (prefab == null) return;

        if (_currentPrefabKey != prefab.gameObject)
        {
            ClearActiveIndicator();
            _currentPrefabKey = prefab.gameObject;
        }

        if (!_indicatorPools.ContainsKey(_currentPrefabKey))
        {
            var newPool = new ObjectPooler<Reference>(prefab,transform,10);
            _indicatorPools.Add(_currentPrefabKey, newPool);
        }

        if (_activeIndicator == null)
        {
            _activeIndicator = _indicatorPools[_currentPrefabKey].Get(transform.position, Quaternion.identity);
        }

        Transform finalTargetPivot = target;
        Vector3 finalOffset = offset;

        if (target != null)
        {
            // 🔥 BƯỚC 1: Ưu tiên tìm kiếm "HitPoint" ẩn sâu trong mọi cấp con (kể cả trong Armature)
            Transform deepHitPoint = FindChildDeep(target, "HitPoint");

            if (deepHitPoint != null)
            {
                finalTargetPivot = deepHitPoint;
                finalOffset = Vector3.zero; // Găm trực tiếp vào tâm HitPoint
            }
            else
            {
                // 🔥 BƯỚC 2: Nếu không có HitPoint mới check xương Humanoid Chest làm phương án dự phòng
                Animator animator = target.GetComponentInChildren<Animator>();
                if (animator != null && animator.isHuman)
                {
                    Transform chestBone = animator.GetBoneTransform(HumanBodyBones.Chest);
                    if (chestBone != null)
                    {
                        finalTargetPivot = chestBone;
                        finalOffset = Vector3.zero;
                    }
                }
                else
                {
                    // Phương án dự phòng cuối cùng nếu cả 2 đều thất bại
                    finalOffset = offset == Vector3.zero ? new Vector3(0f, 1.5f, 0f) : offset;
                }
            }
        }

        // Tự động đồng bộ scale theo lossyScale của điểm tìm được
        Vector3 finalScale = finalTargetPivot != null ? finalTargetPivot.lossyScale : Vector3.one;

        if (_activeIndicator != null)
        {
            IIndicatorBehaviour behaviour = _activeIndicator.GetComponent<IIndicatorBehaviour>();
            if (behaviour != null)
            {
                behaviour.Initialize(transform, finalTargetPivot, finalOffset, finalScale);
            }
        }
    }

    private void LateUpdate()
    {
        if (_activeIndicator != null)
        {
            IIndicatorBehaviour behaviour = _activeIndicator.GetComponent<IIndicatorBehaviour>();
            behaviour?.Tick();
        }
    }

    public void ClearActiveIndicator()
    {
        if (_activeIndicator != null)
        {
            _activeIndicator.Release();
            _activeIndicator = null;
        }
        _currentPrefabKey = null;
    }

    private Reference GetPrefabFromDatabase(IndicatorKey key)
    {
        for (int i = 0; i < indicatorDatabase.Count; i++)
        {
            if (indicatorDatabase[i].key == key) return indicatorDatabase[i].prefab;
        }
        return null;
    }

    /// <summary>
    /// 🔥 HÀM PHỤ TRỢ: Tìm kiếm đệ quy một Object con theo tên, xuyên qua mọi cấp độ sâu
    /// </summary>
    private Transform FindChildDeep(Transform parent, string childName)
    {
        // Kiểm tra xem có trùng tên ngay cấp này không
        if (parent.name == childName) return parent;

        // Quét qua từng đứa con trực tiếp
        foreach (Transform child in parent)
        {
            // Gọi đệ quy để đào sâu vào trong đứa con đó
            Transform result = FindChildDeep(child, childName);

            // Nếu tìm thấy ở tầng sâu hơn thì trả kết quả về luôn
            if (result != null) return result;
        }

        // Hoàn toàn không tìm thấy
        return null;
    }
}