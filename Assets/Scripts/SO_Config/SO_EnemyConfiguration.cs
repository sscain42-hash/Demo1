using UnityEngine;

[CreateAssetMenu(fileName = "Enemy Config", menuName = "Characters Configuration/Enemy")]
public class SO_EnemyConfiguration : SO_CharacterConfiguration
{
    
    [Header("STATS MULTIPLIER")]
    [Tooltip("Tỉ lệ HP của Enemy so với người chơi"), SerializeField]
    private float HPRatio;
   
    
    // Func
    public float GetHPRatio() => HPRatio;
    public void SetHPRatio(float _value) => HPRatio = _value;
    
 
}
