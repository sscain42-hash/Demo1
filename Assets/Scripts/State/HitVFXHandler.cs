using System.Collections.Generic;
using UnityEngine;

public class HitVFXHandler : MonoBehaviour
{
    public static HitVFXHandler Instance;

    private Dictionary<GameObject, ObjectPooler<Reference>> _vfxPools = new Dictionary<GameObject, ObjectPooler<Reference>>();
   [SerializeField] private GameObject prefab;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // Hàm này dùng để gán vào UnityEvent trong Inspector
    // Lưu ý: UnityEvent chỉ hỗ trợ tối đa 1 tham số trong Inspector.
    // Nếu bạn muốn truyền prefab, bạn nên có các hàm riêng cho từng loại Prefab 
    // hoặc tạo một Script trung gian.

    // Cách linh hoạt nhất cho Inspector:
    public void PlayHitVFX( Vector3 position)
    {
        if (prefab == null) return;

        if (!_vfxPools.ContainsKey(prefab))
        {
            CreatePoolForPrefab(prefab);
        }

        Reference instance = _vfxPools[prefab].Get(position, Quaternion.identity);
        instance.gameObject.SetActive(true);

        var ps = instance.GetComponent<ParticleSystem>();
        if (ps != null) ps.Play();
    }

    private void CreatePoolForPrefab(GameObject prefab)
    {
        GameObject template = Instantiate(prefab);
        template.SetActive(false);
        Reference refComp = template.GetComponent<Reference>();
        if (refComp == null) refComp = template.AddComponent<Reference>();
        _vfxPools[prefab] = new ObjectPooler<Reference>(refComp, transform, 5);
    }
}