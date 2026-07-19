using UnityEngine;
using System.Collections;

public class EffectManager : MonoBehaviour
{
    [SerializeField] private Renderer targetRenderer;
    private MaterialPropertyBlock _propBlock;

    void Awake() => _propBlock = new MaterialPropertyBlock();

    // Hàm chung để chạy bất kỳ hiệu ứng nào
    public void PlayEffect(MaterialEffectProfile profile)
    {
        StartCoroutine(EffectRoutine(profile));
        Debug.Log($"Effect {profile.name} started on {targetRenderer.name}");
    }

    private IEnumerator EffectRoutine(MaterialEffectProfile profile)
    {
        targetRenderer.GetPropertyBlock(_propBlock);

        int propertyID = Shader.PropertyToID(profile.propertyName);

        // Áp dụng màu hiệu ứng
        _propBlock.SetColor(propertyID, profile.color);
        targetRenderer.SetPropertyBlock(_propBlock);

        yield return new WaitForSeconds(profile.duration);

        // Reset về mặc định (giả sử là màu đen/tắt)
        _propBlock.SetColor(propertyID, Color.black);
        targetRenderer.SetPropertyBlock(_propBlock);
    }
}