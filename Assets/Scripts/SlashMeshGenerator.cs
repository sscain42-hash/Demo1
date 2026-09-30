using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SlashMeshGenerator : MonoBehaviour
{
    private SlashDataSO _slashData;
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private Mesh _mesh;
    private float _fadeTimer = 0f;
    private float _fadeDuration = 0.2f;
    private bool _isFading = false;

    private void Awake()
    {
        _meshFilter = GetComponent<MeshFilter>();
        _meshRenderer = GetComponent<MeshRenderer>();
        _mesh = new Mesh { name = "RuntimeSlashMesh" };
        _meshFilter.mesh = _mesh;
    }

    public void SetData(SlashDataSO data)
    {
        _slashData = data;
        if (_slashData != null && _slashData.slashMaterial != null)
        {
            _meshRenderer.material = _slashData.slashMaterial;
        }
    }

    public void UpdateProgress(float progress)
    {
        // Gọi trực tiếp Class Utility dùng chung
        SlashMeshUtility.GenerateMesh(_slashData, _mesh, progress);
    }

    public void StartFadeAndDestroy(float duration = 0.2f)
    {
        _fadeDuration = duration;
        _isFading = true;
        _fadeTimer = 0f;
    }

    private void Update()
    {
        if (!_isFading) return;

        _fadeTimer += Time.deltaTime;
        float alpha = 1f - Mathf.Clamp01(_fadeTimer / _fadeDuration);

        if (_meshRenderer.material != null && _meshRenderer.material.HasProperty("_Color"))
        {
            Color c = _meshRenderer.material.color;
            c.a = alpha;
            _meshRenderer.material.color = c;
        }

        if (_fadeTimer >= _fadeDuration)
        {
            Destroy(gameObject);
        }
    }
}