using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewAttackData", menuName = "Player/Attack Data")]
public class AttackData : ScriptableObject 
{
    public string animationName;
    public List<ActionWindow> windows;
    [Header("Basic")]
    public float damage;

    [Header("Step Forward")]
    public bool useStep = true;
    public float stepDistance = 0.6f;
    public float stepSpeed = 10f;
    public AnimationCurve stepCurve = AnimationCurve.EaseInOut(0, 0.5f, 1, 1f);
}

