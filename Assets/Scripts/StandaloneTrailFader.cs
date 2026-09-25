using UnityEngine;
// =======================================================
// LỚP FADER CÓ HỖ TRỢ POOLING
// =======================================================
public class StandaloneTrailFader : MonoBehaviour
{
    private Mesh _targetMesh;
    private MeshRenderer _targetRenderer;
    private MaterialPropertyBlock _propBlock;
    private Color[] _initialColors;
    private Color[] _currentColors;

    private float _duration;
    private float _timer;
    private bool _isFading;
    private System.Action<GameObject> _onCompleteCallback;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    public void StartFade(float duration, Mesh mesh, MeshRenderer renderer, System.Action<GameObject> onComplete)
    {
        _duration = duration;
        _timer = duration;
        _targetMesh = mesh;
        _targetRenderer = renderer;
        _onCompleteCallback = onComplete;
        _propBlock = new MaterialPropertyBlock();

        if (_targetMesh != null && _targetMesh.colors != null && _targetMesh.colors.Length > 0)
        {
            _initialColors = _targetMesh.colors;
            _currentColors = new Color[_initialColors.Length];
        }

        _isFading = true;
    }

    private void Update()
    {
        if (!_isFading) return;

        _timer -= Time.deltaTime;
        float fadeRatio = Mathf.Clamp01(_timer / _duration);

        if (_targetMesh != null && _initialColors != null)
        {
            for (int i = 0; i < _initialColors.Length; i++)
            {
                Color c = _initialColors[i];
                c.a = _initialColors[i].a * fadeRatio;
                _currentColors[i] = c;
            }
            _targetMesh.colors = _currentColors;
        }

        if (_targetRenderer != null)
        {
            _targetRenderer.GetPropertyBlock(_propBlock);
            Color matColor = Color.white;
            matColor.a = fadeRatio;

            _propBlock.SetColor(BaseColorId, matColor);
            _propBlock.SetColor(ColorId, matColor);

            _targetRenderer.SetPropertyBlock(_propBlock);
        }

        if (_timer <= 0)
        {
            _isFading = false;
            _onCompleteCallback?.Invoke(gameObject);
        }
    }
}