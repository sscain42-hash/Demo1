
using UnityEngine;

[System.Serializable]
public class ControlPoint
{
    [Header("Local Transformations")]
    public Vector3 position; // Position tương đối so với Transform
    public Quaternion rotation = Quaternion.identity; // Rotation tương đối so với Transform
    public float fanHeight = 2.0f;

    public ControlPoint(Vector3 pos, Quaternion rot, float height = 2.0f)
    {
        position = pos;
        rotation = rot;
        fanHeight = height;
    }
}