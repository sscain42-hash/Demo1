using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewBezierPathData", menuName = "Slash/Bezier Path Data")]
public class BezierPathData : ScriptableObject
{
    [System.Serializable]
    public struct FrameNode
    {
        public float time;
        public Vector3 pos1;
        public Vector3 pos2;
        public Quaternion rot1;
        public Quaternion rot2;
    }

    public List<FrameNode> frameNodes = new List<FrameNode>();
}