using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Hệ thống Né Hoàn Hảo (Perfect Dodge) kiểu Bayonetta.
/// Khi đòn đánh chạm trúng player trong lúc dash (i-frame):
///  - Time.timeScale giảm mạnh trong slowMoDuration giây (realtime)
///  - Nhân vật để lại tàn ảnh theo chu kỳ suốt khoảng slow-mo
/// Tự động tạo instance nếu scene chưa có.
/// </summary>
public class PerfectDodgeSystem : MonoBehaviour
{
    public static PerfectDodgeSystem Instance { get; private set; }

    [Header("=== Slow Motion ===")]
    [Tooltip("TimeScale khi slow-mo (thế giới chậm lại)")]
    [SerializeField] private float slowMoTimeScale = 0.15f;

    [Tooltip("Thời lượng slow-mo tính bằng giây THỰC (realtime)")]
    [SerializeField] private float slowMoDuration = 0.9f;

    [Tooltip("Thời gian blend timeScale từ slow về 1 ở cuối hiệu ứng")]
    [SerializeField] private float rampBackDuration = 0.15f;

    [Header("=== Cooldown ===")]
    [Tooltip("Cooldown giữa 2 lần perfect dodge (giây realtime)")]
    [SerializeField] private float perfectDodgeCooldown = 0.8f;

    [Header("=== Player Advantage ===")]
    [Tooltip("Tốc độ PLAYER trong slow-mo so với bình thường (0-1). VD 0.6 = player chạy 60% tốc độ thường trong khi world chỉ còn 15% → player có lợi thế lớn")]
    [Range(0f, 1f)]
    [SerializeField] private float playerTimeScale = 0.6f;

    [Header("=== Afterimage (Tàn Ảnh) ===")]
    [Tooltip("Chu kỳ spawn tàn ảnh (giây realtime)")]
    [SerializeField] private float afterimageInterval = 0.06f;

    [Tooltip("Thời gian tàn ảnh tồn tại trước khi mờ hết (giây realtime)")]
    [SerializeField] private float afterimageLifetime = 0.5f;

    [Tooltip("Màu tàn ảnh")]
    [SerializeField] private Color afterimageColor = new Color(0.6f, 0.85f, 1f, 0.45f);

    [Header("=== SFX ===")]
    [SerializeField] private AudioClip perfectDodgeSFX;

    // ═══════════════════════════════════════════════════════════
    // RUNTIME STATE
    // ═══════════════════════════════════════════════════════════
    private bool _slowMoActive;
    private float _appliedTimeScale = 1f;
    private float _lastTriggerRealtime;

    // Player-time compensation
    private float _playerTimeScaleCurrent = 1f;
    private Animator _playerAnimator;
    private float _originalAnimatorSpeed = 1f;

    private Material _ghostMaterial;
    private SkinnedMeshRenderer[] _cachedRenderers;

    /// <summary>TimeScale hệ thống này đang áp dụng (cho HitStopSystem restore đúng)</summary>
    public float CurrentTimeScale => _slowMoActive ? _appliedTimeScale : 1f;
    public bool IsSlowMoActive => _slowMoActive;

    /// <summary>
    /// Hệ số nhân deltaTime cho PLAYER trong slow-mo.
    /// > 1 nghĩa là player trải nghiệm thời gian nhanh hơn world đang bị kéo chậm.
    /// </summary>
    public static float PlayerDtMultiplier
    {
        get
        {
            var inst = Instance;
            if (inst == null || !inst._slowMoActive) return 1f;
            return inst._playerTimeScaleCurrent / Mathf.Max(inst._appliedTimeScale, 0.0001f);
        }
    }

    /// <summary>DeltaTime theo "thời gian player" — mọi logic thuộc player nên dùng cái này.</summary>
    public static float PlayerDeltaTime => Time.deltaTime * PlayerDtMultiplier;

    // ═══════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════════════
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        _ghostMaterial = CreateGhostMaterial();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_ghostMaterial != null) Destroy(_ghostMaterial);
    }

    private void OnDisable()
    {
        if (_slowMoActive)
        {
            StopAllCoroutines();
            Time.timeScale = 1f;
            _slowMoActive = false;
            _appliedTimeScale = 1f;
            _playerTimeScaleCurrent = 1f;
            RestoreAnimatorSpeed();
            _cachedRenderers = null;
        }
    }

    // ═══════════════════════════════════════════════════════════
    // PUBLIC API — gọi từ PlayerController.TakeDMG khi đòn bị dash hủy
    // ═══════════════════════════════════════════════════════════
    public static void NotifyPerfectDodge(Transform modelRoot)
    {
        var sys = Instance;
        if (sys == null)
        {
            var go = new GameObject("PerfectDodgeSystem");
            sys = go.AddComponent<PerfectDodgeSystem>();
        }
        sys.Trigger(modelRoot);
    }

    private void Trigger(Transform modelRoot)
    {
        float now = Time.unscaledTime;
        if (_slowMoActive) return;
        if (now - _lastTriggerRealtime < perfectDodgeCooldown) return;

        _lastTriggerRealtime = now;
        _slowMoActive = true;

        CacheAnimator(modelRoot);

        if (perfectDodgeSFX != null)
            SoundManager.Instance?.PlaySFX(perfectDodgeSFX, 0.95f, 1.05f);

        CacheRenderers(modelRoot);
        StartCoroutine(SlowMoRoutine());
    }

    // ═══════════════════════════════════════════════════════════
    // SLOW-MO + AFTERIMAGE LOOP
    // ═══════════════════════════════════════════════════════════
    private IEnumerator SlowMoRoutine()
    {
        ApplyTimeScale(slowMoTimeScale);
        _playerTimeScaleCurrent = playerTimeScale;
        UpdateAnimatorSpeed();
        SpawnAfterimages(); // ghost ngay tại frame trigger

        float elapsed = 0f;
        float spawnTimer = afterimageInterval;

        while (elapsed < slowMoDuration)
        {
            float dt = Time.unscaledDeltaTime;
            elapsed += dt;

            spawnTimer -= dt;
            if (spawnTimer <= 0f)
            {
                spawnTimer += afterimageInterval;
                SpawnAfterimages();
            }

            yield return null;
        }

        // Blend timeScale về 1 mượt mà — player time cũng blend về 1
        float ramp = 0f;
        while (ramp < rampBackDuration)
        {
            ramp += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(ramp / rampBackDuration);
            ApplyTimeScale(Mathf.Lerp(slowMoTimeScale, 1f, t));
            _playerTimeScaleCurrent = Mathf.Lerp(playerTimeScale, 1f, t);
            UpdateAnimatorSpeed();
            yield return null;
        }

        ApplyTimeScale(1f);
        _playerTimeScaleCurrent = 1f;
        RestoreAnimatorSpeed();
        _slowMoActive = false;
        _cachedRenderers = null;
    }

    private void ApplyTimeScale(float value)
    {
        _appliedTimeScale = value;
        Time.timeScale = value;
    }

    // ═══════════════════════════════════════════════════════════
    // PLAYER TIME COMPENSATION — animator.speed bù cho player
    // ═══════════════════════════════════════════════════════════
    private void CacheAnimator(Transform modelRoot)
    {
        _playerAnimator = null;
        _originalAnimatorSpeed = 1f;
        if (modelRoot == null) return;

        _playerAnimator = modelRoot.GetComponentInParent<Animator>(true);
        if (_playerAnimator == null)
            _playerAnimator = modelRoot.GetComponentInChildren<Animator>(true);
        if (_playerAnimator != null)
            _originalAnimatorSpeed = _playerAnimator.speed;
    }

    private void UpdateAnimatorSpeed()
    {
        if (_playerAnimator != null)
            _playerAnimator.speed = _originalAnimatorSpeed * PlayerDtMultiplier;
    }

    private void RestoreAnimatorSpeed()
    {
        if (_playerAnimator != null)
            _playerAnimator.speed = _originalAnimatorSpeed;
        _playerAnimator = null;
        _playerTimeScaleCurrent = 1f;
    }

    // ═══════════════════════════════════════════════════════════
    // AFTERIMAGE — bake SkinnedMesh tại pose hiện tại
    // ═══════════════════════════════════════════════════════════
    private void CacheRenderers(Transform modelRoot)
    {
        if (modelRoot == null)
        {
            _cachedRenderers = null;
            return;
        }
        _cachedRenderers = modelRoot.GetComponentsInChildren<SkinnedMeshRenderer>();
    }

    private void SpawnAfterimages()
    {
        if (_cachedRenderers == null || _ghostMaterial == null) return;

        for (int i = 0; i < _cachedRenderers.Length; i++)
        {
            var smr = _cachedRenderers[i];
            if (smr == null || !smr.enabled || smr.sharedMesh == null) continue;

            BakeGhost(smr);
        }
    }

    private void BakeGhost(SkinnedMeshRenderer smr)
    {
        Mesh baked = new Mesh { name = "AfterimageMesh" };
        smr.BakeMesh(baked);
        baked.RecalculateBounds();

        var ghost = new GameObject($"Afterimage_{smr.name}");
        ghost.transform.SetPositionAndRotation(smr.transform.position, smr.transform.rotation);
        ghost.transform.localScale = smr.transform.lossyScale;

        var mf = ghost.AddComponent<MeshFilter>();
        mf.sharedMesh = baked;

        var mr = ghost.AddComponent<MeshRenderer>();
        int subMeshCount = Mathf.Max(1, baked.subMeshCount);
        var mats = new Material[subMeshCount];
        for (int i = 0; i < subMeshCount; i++)
            mats[i] = _ghostMaterial;
        mr.sharedMaterials = mats;

        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;

        ghost.AddComponent<AfterimageGhost>().Init(afterimageColor, afterimageLifetime);
    }

    // ═══════════════════════════════════════════════════════════
    // GHOST MATERIAL — URP Unlit transparent, fallback Standard
    // ═══════════════════════════════════════════════════════════
    private Material CreateGhostMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        bool isUrp = shader != null;
        if (!isUrp) shader = Shader.Find("Unlit/Transparent");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return null;

        var mat = new Material(shader) { name = "PerfectDodgeGhostMaterial" };

        if (isUrp || shader.name == "Unlit/Transparent")
        {
            mat.SetFloat("_Surface", 1f);   // Transparent
            mat.SetFloat("_Blend", 0f);     // Alpha
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.SetColor("_BaseColor", afterimageColor);
            mat.SetColor("_Color", afterimageColor);
        }
        else // Standard legacy
        {
            mat.SetFloat("_Mode", 3f);
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.color = afterimageColor;
        }

        return mat;
    }
}
