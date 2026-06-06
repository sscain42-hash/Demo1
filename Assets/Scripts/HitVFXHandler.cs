using UnityEngine;

public class HitVFXHandler : MonoBehaviour
{
    public static HitVFXHandler Instance;

    [SerializeField] private Reference vfxPrefab; // Kéo Prefab vào đây
    [SerializeField] private int poolSize = 10;

    private ObjectPooler<Reference> _pool;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        InitializePool();
    }

    private void InitializePool()
    {
        if (vfxPrefab == null) return;

        // Tạo parent container cho gọn Hierarchy
        GameObject container = new GameObject($"Pool_{vfxPrefab.name}");
        container.transform.SetParent(transform);

        // Khởi tạo Pool trực tiếp
        _pool = new ObjectPooler<Reference>(vfxPrefab, container.transform, poolSize);
    }

    public void PlayHitVFX(Vector3 position)
    {
        if (_pool == null) return;

        // Lấy từ pool và set vị trí
        Reference instance = _pool.Get(position, Quaternion.identity);

        // Kích hoạt logic VFX
        var ps = instance.GetComponent<ParticleSystem>();
        if (ps != null) ps.Play();
    }
}