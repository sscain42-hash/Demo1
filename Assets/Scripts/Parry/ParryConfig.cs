using UnityEngine;

namespace Combat.Parry
{
    [CreateAssetMenu(fileName = "ParryConfig", menuName = "Combat/Parry Config")]
    public class ParryConfig : ScriptableObject
    {
        [Header("=== PARRY WINDOW ===")]
        [Tooltip("Số frame cửa sổ parry mặc định (60FPS = 12 frame ≈ 0.2s)")]
        [Range(1, 60)]
        public int defaultWindowFrames = 12;

        [Tooltip("Số frame tối thiểu sau khi bị spam penalty (0 = không thể parry)")]
        [Range(0, 60)]
        public int minWindowFrames = 0;

        [Header("=== SPAM PENALTY ===")]
        [Tooltip("Số frame bị trừ mỗi lần spam parry")]
        [Range(0, 30)]
        public int spamPenaltyPerPress = 2;

        [Tooltip("Thời gian cooldown giữa 2 lần nhấn parry (giây)")]
        [Range(0f, 1f)]
        public float parryInputCooldown = 0.1f;

        [Tooltip("Thời gian hồi phục cửa sổ về mặc định nếu không spam (giây)")]
        [Range(0f, 5f)]
        public float windowRecoveryTime = 2f;

        [Tooltip("Số frame hồi phục mỗi giây khi không spam")]
        [Range(0.1f, 10f)]
        public float windowRecoveryRate = 2f;

        [Header("=== CHAIN PARRY ===")]
        [Tooltip("Thời gian grace sau khi parry thành công — đòn kế tiếp tự động được parry")]
        [Range(0f, 1f)]
        public float chainParryGraceTime = 0.3f;

        [Tooltip("Số lần chain parry tối đa liên tiếp")]
        [Range(1, 10)]
        public int maxChainParryCount = 3;

        [Header("=== TELEGRAPH ===")]
        [Tooltip("Nếu true, player chỉ có thể parry khi enemy đang telegraphing")]
        public bool requireTelegraph = false;

        [Tooltip("Thời gian telegraph trước khi hitbox active (giây)")]
        [Range(0f, 2f)]
        public float telegraphDuration = 0.5f;

        // ═══════════════════════════════════════════════════
        // 🔥 PARRY MOVEMENT — Homura Hime Style
        // Input được snapshot NGAY khi parry thành công (không còn window đợi)
        // ═══════════════════════════════════════════════════

        [Header("=== PARRY MOVEMENT ===")]
        [Tooltip("Bật/tắt directional movement sau parry")]
        public bool enableDirectionalJump = true;

        [Tooltip("Tốc độ di chuyển tới (m/s)")]
        [Range(5f, 50f)]
        public float forwardJumpSpeed = 20f;

        [Tooltip("Tốc độ backflip (m/s)")]
        [Range(5f, 50f)]
        public float backwardJumpSpeed = 22f;

        [Tooltip("Tốc độ di chuyển ngang (m/s)")]
        [Range(5f, 50f)]
        public float sideJumpSpeed = 18f;

        [Tooltip("Thời gian di chuyển (giây) — ngắn để dứt khoát")]
        [Range(0.1f, 0.6f)]
        public float jumpDuration = 0.22f;

        [Tooltip("Bật gravity trong lúc parry movement (thường false)")]
        public bool applyGravityDuringMovement = false;

        [Header("=== DEBUG ===")]
        public bool enableDebugLog = true;
        public bool drawDebugGizmos = true;
    }
}
