using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewComboSequence", menuName = "Combo System/Combo Sequence")]
public class ComboSequence : ScriptableObject
{
    [Tooltip("Nạp các file AttackData theo thứ tự chuỗi combo tại đây.")]
    public List<AttackData> attacks = new List<AttackData>();
}
