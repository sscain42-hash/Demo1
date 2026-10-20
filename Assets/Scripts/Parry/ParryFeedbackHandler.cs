using UnityEngine;

namespace Combat.Parry
{
    /// <summary>
    /// Hook vào ParryController events để chạy VFX/SFX tương ứng.
    /// Gắn component này lên cùng GameObject với ParryController.
    /// 
    /// SETUP TRONG INSPECTOR:
    /// - Parry Controller: tự tìm nếu để trống
    /// - Parry Success VFX: prefab VFX (có component Reference)
    /// - Parry Fail VFX: prefab VFX khi parry fail
    /// - Parry Perfect VFX: prefab VFX cho chain parry (đẹp hơn)
    /// - Parry Success SFX: audio clip khi parry thành công
    /// - Parry Fail SFX: audio clip khi parry fail
    /// - Parry Window Open SFX: audio clip khi bắt đầu parry (whoosh)
    /// </summary>
    [RequireComponent(typeof(ParryController))]
    public class ParryFeedbackHandler : MonoBehaviour
    {
        [Header("=== REFERENCES ===")]
        [SerializeField] private ParryController parryController;

        [Header("=== VFX PREFABS ===")]
        [Tooltip("VFX khi parry thành công — thường là shockwave, spark")]
        [SerializeField] private Reference parrySuccessVFX;

        [Tooltip("VFX khi parry fail — thường là dust, nhẹ hơn")]
        [SerializeField] private Reference parryFailVFX;

        [Tooltip("VFX cho chain parry (đòn parry thứ 2 trở đi) — đẹp hơn parry thường")]
        [SerializeField] private Reference parryChainVFX;

        [Tooltip("VFX khi bắt đầu parry (window open) — có thể là charge, glow")]
        [SerializeField] private Reference parryWindowOpenVFX;

        [Header("=== SFX ===")]
        [Tooltip("Âm thanh khi parry thành công — tiếng 'clang' kim loại")]
        [SerializeField] private AudioClip parrySuccessSFX;

        [Tooltip("Âm thanh khi parry fail — tiếng 'whoosh' hoặc 'thud'")]
        [SerializeField] private AudioClip parryFailSFX;

        [Tooltip("Âm thanh khi chain parry — có thể cao hơn parry thường")]
        [SerializeField] private AudioClip parryChainSFX;

        [Tooltip("Âm thanh khi bắt đầu parry (window open)")]
        [SerializeField] private AudioClip parryWindowOpenSFX;

        [Header("=== VFX SPAWN SETTINGS ===")]
        [Tooltip("Offset của VFX so với vị trí player")]
        [SerializeField] private Vector3 vfxOffset = new Vector3(0f, 1.2f, 0f);

        [Tooltip("Offset cho VFX khi spawn tại attacker")]
        [SerializeField] private Vector3 vfxAttackerOffset = new Vector3(0f, 1.2f, 0f);

        [Tooltip("Scale của VFX parry success")]
        [SerializeField] private Vector3 successVFXScale = Vector3.one;

        [Tooltip("Scale của VFX parry chain (to hơn success)")]
        [SerializeField] private Vector3 chainVFXScale = new Vector3(1.3f, 1.3f, 1.3f);

        [Tooltip("Scale của VFX parry fail")]
        [SerializeField] private Vector3 failVFXScale = Vector3.one;

        [Header("=== SFX SETTINGS ===")]
        [Tooltip("Pitch range cho SFX parry success (random trong khoảng này)")]
        [SerializeField] private Vector2 successSFXPitchRange = new Vector2(0.95f, 1.05f);

        [Tooltip("Pitch range cho SFX parry chain (cao hơn success)")]
        [SerializeField] private Vector2 chainSFXPitchRange = new Vector2(1.05f, 1.15f);

        [Tooltip("Pitch range cho SFX parry fail")]
        [SerializeField] private Vector2 failSFXPitchRange = new Vector2(0.9f, 1.0f);

        [Header("=== DEBUG ===")]
        [SerializeField] private bool debugLog = false;

        // =========================================================
        // UNITY LIFECYCLE
        // =========================================================
        private void Awake()
        {
            if (parryController == null)
                parryController = GetComponent<ParryController>();
        }

        private void OnEnable()
        {
            if (parryController == null) return;

            parryController.OnParrySuccess += HandleParrySuccess;
            parryController.OnParryFail += HandleParryFail;
            parryController.OnParryWindowOpened += HandleParryWindowOpened;
            parryController.OnParryWindowClosed += HandleParryWindowClosed;
        }

        private void OnDisable()
        {
            if (parryController == null) return;

            parryController.OnParrySuccess -= HandleParrySuccess;
            parryController.OnParryFail -= HandleParryFail;
            parryController.OnParryWindowOpened -= HandleParryWindowOpened;
            parryController.OnParryWindowClosed -= HandleParryWindowClosed;
        }

        // =========================================================
        // EVENT HANDLERS
        // =========================================================

        /// <summary>
        /// Khi parry thành công — spawn VFX + SFX tại vị trí giữa player và enemy
        /// </summary>
        private void HandleParrySuccess(GameObject attacker)
        {
            bool isChain = parryController.CurrentChainCount > 1;
            var config = parryController.Config;

            Vector3 spawnPos = CalculateVFXPosition(attacker);
            Quaternion spawnRot = CalculateVFXRotation(attacker, spawnPos);

            Reference vfxToSpawn = isChain && parryChainVFX != null ? parryChainVFX : parrySuccessVFX;
            Vector3 vfxScale = isChain ? chainVFXScale : successVFXScale;

            if (vfxToSpawn != null)
                GlobalVFXManager.SpawnGlobalVFX(vfxToSpawn, spawnPos, spawnRot, vfxScale);

            AudioClip sfxToPlay = isChain && parryChainSFX != null ? parryChainSFX : parrySuccessSFX;
            Vector2 pitchRange = isChain ? chainSFXPitchRange : successSFXPitchRange;

            if (sfxToPlay != null && SoundManager.Instance != null)
                SoundManager.Instance.PlaySFX(sfxToPlay, pitchRange.x, pitchRange.y, 0.05f, 1f);

            if (config != null)
            {
                if (config.enableHitStopOnParry && HitStopSystem.Instance != null)
                    HitStopSystem.Instance.Trigger(config.parryHitStopDuration, config.parryHitStopTimeScale);

                if (config.enableScreenShakeOnParry && ScreenShakeManager.Instance != null)
                {
                    float force = isChain ? config.chainParryScreenShakeForce : config.parryScreenShakeForce;
                    ScreenShakeManager.Instance.TriggerShake(force);
                }
            }
        }

        /// <summary>
        /// Khi parry fail — VFX/SFX nhẹ hơn
        /// </summary>
        private void HandleParryFail()
        {
            if (debugLog)
                Debug.Log("[ParryFeedback] Parry FAIL");

            // Vị trí player
            Vector3 spawnPos = transform.position + vfxOffset;
            Quaternion spawnRot = transform.rotation;

            // Spawn VFX fail
            if (parryFailVFX != null)
            {
                GlobalVFXManager.SpawnGlobalVFX(parryFailVFX, spawnPos, spawnRot, failVFXScale);

                if (debugLog)
                    Debug.Log($"[ParryFeedback] Spawned FAIL VFX at {spawnPos}");
            }

            // Play SFX fail
            if (parryFailSFX != null && SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySFX(
                    parryFailSFX,
                    minPitch: failSFXPitchRange.x,
                    maxPitch: failSFXPitchRange.y,
                    volumeVariance: 0.1f,
                    baseVolumeScale: 0.8f
                );

                if (debugLog)
                    Debug.Log("[ParryFeedback] Played FAIL SFX");
            }
        }

        /// <summary>
        /// Khi bắt đầu parry — chạy SFX/VFX nhẹ báo hiệu
        /// </summary>
        private void HandleParryWindowOpened(int frames)
        {
            if (debugLog)
                Debug.Log($"[ParryFeedback] Window opened — {frames} frames");

            // Spawn VFX window open (optional)
            if (parryWindowOpenVFX != null)
            {
                Vector3 spawnPos = transform.position + vfxOffset;
                Quaternion spawnRot = transform.rotation;

                GlobalVFXManager.SpawnGlobalVFX(parryWindowOpenVFX, spawnPos, spawnRot);
            }

            // Play SFX window open (optional)
            if (parryWindowOpenSFX != null && SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySFX(
                    parryWindowOpenSFX,
                    minPitch: 0.95f,
                    maxPitch: 1.05f,
                    volumeVariance: 0.05f,
                    baseVolumeScale: 0.7f
                );
            }
        }

        /// <summary>
        /// Khi window đóng — có thể dùng để cleanup VFX loop nếu có
        /// </summary>
        private void HandleParryWindowClosed()
        {
            if (debugLog)
                Debug.Log("[ParryFeedback] Window closed");
        }

        // =========================================================
        // HELPER METHODS
        // =========================================================

        /// <summary>
        /// Tính vị trí spawn VFX — giữa player và attacker, hơi cao hơn
        /// </summary>
        private Vector3 CalculateVFXPosition(GameObject attacker)
        {
            if (attacker == null)
                return transform.position + vfxOffset;

            // Vị trí giữa player và attacker
            Vector3 midPoint = (transform.position + attacker.transform.position) * 0.5f;

            // Nâng lên một chút
            midPoint.y = transform.position.y + 1.2f;

            return midPoint;
        }

        /// <summary>
        /// Tính rotation VFX — hướng về attacker
        /// </summary>
        private Quaternion CalculateVFXRotation(GameObject attacker, Vector3 spawnPos)
        {
            if (attacker == null)
                return transform.rotation;

            Vector3 dirToAttacker = attacker.transform.position - spawnPos;
            dirToAttacker.y = 0f;

            if (dirToAttacker.sqrMagnitude < 0.001f)
                return transform.rotation;

            return Quaternion.LookRotation(dirToAttacker.normalized);
        }

        // =========================================================
        // DEBUG
        // =========================================================

        [ContextMenu("Test Parry Success VFX")]
        private void TestSuccessVFX()
        {
            HandleParrySuccess(null);
        }

        [ContextMenu("Test Parry Fail VFX")]
        private void TestFailVFX()
        {
            HandleParryFail();
        }

        [ContextMenu("Test Parry Window Open")]
        private void TestWindowOpen()
        {
            HandleParryWindowOpened(12);
        }
    }
}
