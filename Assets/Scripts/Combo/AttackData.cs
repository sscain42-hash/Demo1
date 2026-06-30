using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewAttackData", menuName = "Combat/Attack Data")]
public class AttackData : ScriptableObject
{
    public string animationName;
    public List<ActionWindow> windows = new List<ActionWindow>();

 
}