using UnityEngine;

public class BezierPathRecorder : MonoBehaviour
{
    [SerializeField] private Lerper larper;
    [SerializeField] private BezierPathData dataToSave;

    [ContextMenu("Record Frame Node")]
    public void RecordCurrentFrame(float time)
    {
        if (dataToSave == null || larper == null) return;

        BezierPathData.FrameNode node = new BezierPathData.FrameNode
        {
            time = time,
            pos1 = larper.Pos1,
            pos2 = larper.Pos2,

            // Dùng Quaternion.Euler() để chuyển đổi từ Vector3 góc Euler sang Quaternion
            rot1 = Quaternion.Euler(larper.Rot1),
            rot2 = Quaternion.Euler(larper.Rot2)

            // Hoặc có thể gọi rút gọn bằng getter đã tạo trong Larper.cs:
            // rot1 = larper.Rot_1,
            // rot2 = larper.Rot_2
        };

        dataToSave.frameNodes.Add(node);

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(dataToSave);
#endif
    }
}