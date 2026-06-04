using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AttackData", menuName = "Combo System/Attack Data")]
public class AttackData : ScriptableObject
{
    public string animationName;
    public float damage; // Chỉ giữ chỉ số damage gốc

    // ❌ ĐÃ XÓA BIẾN projectilePrefab TẠI ĐÂY ❌

    public List<ActionWindow> windows;
 
}