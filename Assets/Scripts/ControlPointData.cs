using UnityEngine;

[System.Serializable]
public struct ControlPointData
{
    public Vector3 localPosition;
    public Vector3 localEulerAngles; // Dùng EulerAngles trong SO cho dễ chỉnh trên Inspector
    public float fanHeight;

    public Quaternion LocalRotation => Quaternion.Euler(localEulerAngles);

    public ControlPointData(Vector3 pos, Vector3 euler, float height)
    {
        localPosition = pos;
        localEulerAngles = euler;
        fanHeight = height;
    }
}

