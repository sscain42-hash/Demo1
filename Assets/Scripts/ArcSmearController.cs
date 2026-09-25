using System.Collections;
using UnityEngine;

public class ArcSmearController : MonoBehaviour
{
    [Header("Target Mesh")]
    [SerializeField] private MeshRenderer smearMeshRenderer;

    [Header("Animation Settings")]
    [Tooltip("Thời gian tồn tại của vệt chém (Nên đặt từ 0.12s - 0.2s)")]
    [SerializeField] private float duration = 0.15f;

    [Tooltip("Đồ thị biến thiên độ mờ (Fade In nhanh -> Fade Out)")]
    [SerializeField] private AnimationCurve alphaCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

    [Tooltip("Đồ thị phóng to/thu nhỏ vệt chém để tạo cảm giác lực chém (Scale Pop)")]
    [SerializeField]
    private AnimationCurve scaleCurve = new AnimationCurve(
        new Keyframe(0f, 0.7f),
        new Keyframe(0.3f, 1.1f),
        new Keyframe(1f, 1.0f)
    );

    [Header("Shader Property Names")]
    [SerializeField] private string progressPropertyName = "_Progress";
    [SerializeField] private string colorPropertyName = "_BaseColor";

    private MaterialPropertyBlock _propBlock;
    private Coroutine _smearCoroutine;
    private Vector3 _originalScale;

    private static readonly int ProgressId = Shader.PropertyToID("_Progress");
    private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int LegacyColorId = Shader.PropertyToID("_Color");

    private void Awake()
    {
        if (smearMeshRenderer != null)
        {
            _originalScale = smearMeshRenderer.transform.localScale;
            smearMeshRenderer.enabled = false;
        }
        _propBlock = new MaterialPropertyBlock();
    }

    /// <summary>
    /// Gọi hàm này từ Animation Event (ví dụ: PerfectSwordSmearOneEnable)
    /// </summary>
    public void PlaySmear()
    {
        if (smearMeshRenderer == null) return;

        if (_smearCoroutine != null)
            StopCoroutine(_smearCoroutine);

        _smearCoroutine = StartCoroutine(AnimateSmearRoutine());
    }

    private IEnumerator AnimateSmearRoutine()
    {
        smearMeshRenderer.enabled = true;
        Transform meshTransform = smearMeshRenderer.transform;

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(timer / duration);

            // 1. Quét UV đường chém từ 0 đến 1 (Progress)
            float progress = normalizedTime;

            // 2. Tính Alpha mờ dần theo thời gian
            float alpha = alphaCurve.Evaluate(normalizedTime);

            // 3. Tính Scale biến thiên (Scale Pop tạo lực)
            float scaleMultiplier = scaleCurve.Evaluate(normalizedTime);
            meshTransform.localScale = _originalScale * scaleMultiplier;

            // 4. Cập nhật thuộc tính vào Shader không gây hao tốn bộ nhớ (MaterialPropertyBlock)
            smearMeshRenderer.GetPropertyBlock(_propBlock);
            _propBlock.SetFloat(ProgressId, progress);

            Color c = Color.white;
            c.a = alpha;
            _propBlock.SetColor(ColorId, c);
            _propBlock.SetColor(LegacyColorId, c);

            smearMeshRenderer.SetPropertyBlock(_propBlock);

            yield return null;
        }

        // Tắt Mesh sau khi kết thúc hiệu ứng
        smearMeshRenderer.enabled = false;
        meshTransform.localScale = _originalScale;
    }
}