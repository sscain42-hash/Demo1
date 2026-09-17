using UnityEngine;

[System.Serializable]
public struct TransformData

{
    public Vector3 positionOffset;
    public Vector3 rotationOffset;
    public Vector3 scale;

    // Giá trị mặc định để scale không bị bằng 0 khi mới tạo
    public static TransformData Default => new TransformData
    {
        scale = Vector3.one
    };
}