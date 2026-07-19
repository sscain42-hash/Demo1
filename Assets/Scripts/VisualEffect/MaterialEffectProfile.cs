using UnityEngine;

[CreateAssetMenu(menuName = "Effects/Material Effect Profile")]
public class MaterialEffectProfile : ScriptableObject
{
    public Color color = Color.white;
    public float duration = 0.2f; // Thời gian hiệu ứng kéo dài
    public string propertyName = "_EmissionColor";
}