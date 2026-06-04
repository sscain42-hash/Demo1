using UnityEngine;

[System.Serializable]
public struct VFXTransformData
{
    public Vector3 positionOffset;
    public Vector3 rotationOffset;
    public Vector3 scale;

    // Giá trị mặc định để scale không bị bằng 0 khi mới tạo
    public static VFXTransformData Default => new VFXTransformData
    {
        scale = Vector3.one
    };
}