using UnityEngine;

/// <summary>
/// Ghost tàn ảnh — fade out theo thời gian REALTIME (unscaled)
/// để tàn ảnh vẫn mờ dần đều kể cả khi đang slow-mo.
/// </summary>
public class AfterimageGhost : MonoBehaviour
{
    private float _lifetime = 0.5f;
    private float _timer;
    private Color _color;
    private MeshRenderer _renderer;
    private MaterialPropertyBlock _propBlock;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int LegacyColorId = Shader.PropertyToID("_Color");

    public void Init(Color color, float lifetime)
    {
        _color = color;
        _lifetime = Mathf.Max(0.01f, lifetime);
        _timer = 0f;
        _renderer = GetComponent<MeshRenderer>();
        _propBlock = new MaterialPropertyBlock();
    }

    private void Update()
    {
        _timer += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(_timer / _lifetime);

        if (_renderer != null)
        {
            Color c = _color;
            c.a = _color.a * (1f - t);

            _renderer.GetPropertyBlock(_propBlock);
            _propBlock.SetColor(BaseColorId, c);
            _propBlock.SetColor(LegacyColorId, c);
            _renderer.SetPropertyBlock(_propBlock);
        }

        if (t >= 1f)
        {
            var mf = GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
                Destroy(mf.sharedMesh);
            Destroy(gameObject);
        }
    }
}
