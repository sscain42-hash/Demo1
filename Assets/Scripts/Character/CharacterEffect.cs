using System;
using System.Collections.Generic;
using UnityEngine;

public class CharacterEffect : MonoBehaviour, IAttack
{
    [Tooltip("Script điều khiển chính (Chỉ Player có, Enemy để trống)"), SerializeField]
    private PlayerController _ctx;

    [Header("⚔️ DETECTOR CẬN CHIẾN TRÊN NGƯỜI")]
    [SerializeField] private PhysicsDetection NA_Detection;
    [SerializeField] private HitboxDetection CA_Detection;
    [SerializeField] private ParticleDetection ES_Detection;
    [SerializeField] private ParticleDetection EB_Detection;

    private Dictionary<GameObject, ObjectPooler<Reference>> _dynamicPools = new Dictionary<GameObject, ObjectPooler<Reference>>();
    private AttackType _currentFiringAttackType = AttackType.NormalAttack;

    // 🔥 ĐỐI TƯỢNG NEO GIỮ CHUNG Ở WORLD SPACE (TRÁNH LÀM CON CỦA PLAYER) 🔥
    private static Transform _projectilePoolAnchor;

    private void Awake()
    {
        // Đăng ký các đòn cận chiến cơ bản
        if (NA_Detection != null) NA_Detection.CollisionEnterEvent.AddListener(Detection_NA);
        if (CA_Detection != null) CA_Detection.CollisionEnterEvent.AddListener(Detection_CA);
        if (ES_Detection != null) ES_Detection.CollisionEnterEvent.AddListener(Detection_E);
        if (EB_Detection != null) EB_Detection.CollisionEnterEvent.AddListener(Detection_Q);

        // Khởi tạo Anchor ở không gian thế giới nếu chưa có ai tạo
        InitializeWorldAnchor();
    }

    /// <summary>
    /// Tự động sinh ra một Empty Object quản lý chung ngoài Hierarchy thế giới
    /// </summary>
    private void InitializeWorldAnchor()
    {
        if (_projectilePoolAnchor == null)
        {
            GameObject anchor = GameObject.Find("[DYNAMIC_PROJECTILE_POOL]");
            if (anchor == null)
            {
                anchor = new GameObject("[DYNAMIC_PROJECTILE_POOL]");
                // Đảm bảo không dính líu đến bất kỳ Parent nào khác
                anchor.transform.SetParent(null);
            }
            _projectilePoolAnchor = anchor.transform;
        }
    }

    /// <summary>
    /// Hàm xuất đạn thông minh từ AttackData - Hoàn toàn độc lập Transform với Player
    /// </summary>
    public Reference SpawnProjectileFromData(Reference prefabFromData, Vector3 position, Quaternion rotation, AttackType type)
    {
        if (prefabFromData == null) return null;

        _currentFiringAttackType = type;
        GameObject prefabKey = prefabFromData.gameObject;

        // 1. Nếu chưa từng tạo Pool cho loại đạn này -> Tạo và nhét vào World Anchor
        if (!_dynamicPools.ContainsKey(prefabKey))
        {
            int defaultSize = 5;

            // Đảm bảo Anchor thế giới luôn tồn tại an toàn
            InitializeWorldAnchor();

            // 🔥 SỬA TẠI ĐÂY: Thay vì truyền 'transform' (Player), truyền '_projectilePoolAnchor' (World Space)
            var newPool = new ObjectPooler<Reference>(prefabFromData, _projectilePoolAnchor, defaultSize);

            // GÁN EVENT CHO CẢ BỂ CHỨA CỦA LOẠI ĐẠN NÀY
            foreach (var spawnedVFX in newPool.List)
            {
                DetectionBase bulletDetector = spawnedVFX.GetComponent<DetectionBase>();
                if (bulletDetector != null)
                {
                    bulletDetector.CollisionEnterEvent.AddListener(HandleHit);
                   
                }
            }

            _dynamicPools.Add(prefabKey, newPool);
        }

        // 2. Lấy đạn ra từ đúng Pool thế giới của nó
        Reference spawnedInstance = _dynamicPools[prefabKey].Get(position, rotation);

        // BIỆN PHÁP AN TOÀN TUYỆT ĐỐI: Ép đạn ngắt hoàn toàn liên kết Parent nếu ObjectPooler có logic tự động gán
        if (spawnedInstance != null && spawnedInstance.transform.parent != _projectilePoolAnchor)
        {
            spawnedInstance.transform.SetParent(_projectilePoolAnchor);
        }

        return spawnedInstance;
    }

    private void HandleHit(GameObject victim)
    {
        if (gameObject.CompareTag("Player") && !victim.CompareTag("Enemy")) return;
        if (gameObject.CompareTag("Enemy") && !victim.CompareTag("Player")) return;

        if (_ctx != null)
        {
            switch (_currentFiringAttackType)
            {
                case AttackType.NormalAttack: Detection_NA(victim); break;
                case AttackType.ChargedAttack: Detection_CA(victim); break;
                case AttackType.E: Detection_E(victim); break;
                case AttackType.Q: Detection_Q(victim); break;
            }
        }
    }

    public void CheckNACollision() => NA_Detection?.CheckCollision();
    public void CheckCACollision() => CA_Detection?.CheckCollision();

    public void Detection_NA(GameObject _gameObject) => _ctx?.CauseDMG(_gameObject, AttackType.NormalAttack);
    public void Detection_CA(GameObject _gameObject) => _ctx?.CauseDMG(_gameObject, AttackType.ChargedAttack);
    public void Detection_E(GameObject _gameObject) => _ctx?.CauseDMG(_gameObject, AttackType.E);
    public void Detection_Q(GameObject _gameObject) => _ctx?.CauseDMG(_gameObject, AttackType.Q);

    private void OnDestroy()
    {
        foreach (var pool in _dynamicPools.Values)
        {
            foreach (var spawnedVFX in pool.List)
            {
                if (spawnedVFX == null) continue;
                DetectionBase bulletDetector = spawnedVFX.GetComponent<DetectionBase>();
                if (bulletDetector != null) bulletDetector.CollisionEnterEvent.RemoveListener(HandleHit);
            }
        }
    }
}

