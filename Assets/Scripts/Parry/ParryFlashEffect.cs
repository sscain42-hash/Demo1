using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
/// <summary>
/// Singleton quản lý hiệu ứng flash trắng khi parry thành công.
/// 
/// Setup trong Scene:
///   1. Tạo Canvas (Screen Space - Overlay), sortOrder cao (999)
///   2. Thêm Image con, màu trắng, alpha = 0, phủ toàn màn hình (RectTransform stretch)
///   3. Gắn script này vào Canvas, kéo Image vào field flashImage
///   4. Kéo ShockwaveVFX, SparkVFX prefab (có component Reference) vào
///   5. Kéo ClangSFX AudioClip vào
/// </summary>
public class ParryFlashEffect : MonoBehaviour
{
    public static ParryFlashEffect Instance { get; private set; }

    [Header("Flash UI")]
    [Tooltip("Image trắng phủ toàn màn hình. Alpha sẽ fade 1 → 0 trong flashDuration.")]
    [SerializeField] private UnityEngine.UI.Image flashImage;

    [Tooltip("Thời gian fade từ alpha 1 → 0")]
    [SerializeField] private float flashDuration = 0.15f;

    [Tooltip("Màu flash (mặc định trắng)")]
    [SerializeField] private Color flashColor = Color.white;

    [Header("VFX Prefabs (gắn component Reference)")]
    [SerializeField] private Reference shockwaveVFXPrefab;
    [SerializeField] private Reference sparkVFXPrefab;

    [Header("SFX")]
    [SerializeField] private AudioClip clangSFX;

    public AudioClip ClangSFX => clangSFX;

    private Coroutine _flashCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Đảm bảo flash image bắt đầu với alpha = 0
        if (flashImage != null)
        {
            Color c = flashColor;
            c.a = 0f;
            flashImage.color = c;
            flashImage.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// Kích hoạt flash trắng (alpha 1 → 0 trong flashDuration).
    /// </summary>
    public void Flash()
    {
        if (flashImage == null) return;

        if (_flashCoroutine != null)
            StopCoroutine(_flashCoroutine);

        _flashCoroutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        // Set alpha = 1 ngay lập tức
        Color c = flashColor;
        c.a = 1f;
        flashImage.color = c;

        float elapsed = 0f;
        while (elapsed < flashDuration)
        {
            elapsed += Time.unscaledDeltaTime; // Dùng unscaled vì có HitStop
            float t = Mathf.Clamp01(elapsed / flashDuration);

            c.a = 1f - t; // Fade out
            flashImage.color = c;

            yield return null;
        }

        c.a = 0f;
        flashImage.color = c;
        _flashCoroutine = null;
    }

    /// <summary>
    /// Spawn shockwave VFX tại vị trí.
    /// </summary>
    public void SpawnShockwave(Vector3 position, Quaternion rotation)
    {
        if (shockwaveVFXPrefab == null) return;
        GlobalVFXManager.SpawnGlobalVFX(shockwaveVFXPrefab, position, rotation);
    }

    /// <summary>
    /// Spawn spark VFX tại vị trí.
    /// </summary>
    public void SpawnSpark(Vector3 position, Quaternion rotation)
    {
        if (sparkVFXPrefab == null) return;
        GlobalVFXManager.SpawnGlobalVFX(sparkVFXPrefab, position, rotation);
    }
}
