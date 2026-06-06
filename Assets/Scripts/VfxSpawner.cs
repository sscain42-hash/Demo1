using UnityEngine;
// ==========================================
// 🚀 LOGIC SPAWN VFX TỪ POOL C# THUẦN
// ==========================================
public class VfxSpawner
{
    private readonly ObjectPooler<Reference> _pooler;

    public VfxSpawner(ObjectPooler<Reference> pooler)
    {
        _pooler = pooler;
    }

    public void SpawnAtPosition(Vector3 position)
    {
        if (_pooler == null) return;

        // Tái sử dụng hàm Get từ ObjectPooler<Reference> của bạn tại vị trí va chạm
        Reference fxInstance = _pooler.Get(position, Quaternion.identity);

        if (fxInstance != null && fxInstance.TryGetComponent(out ParticleSystem ps))
        {
            ps.Play();
        }
    }
}