#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public static class RootMotionExtractor
{
    public struct TrajectoryPoint
    {
        public float time;
        public Vector3 position;
    }

    /// <summary>
    /// Lấy toàn bộ quỹ đạo Root Motion của một clip (trong không gian Local của Root)
    /// </summary>
    public static List<TrajectoryPoint> ExtractTrajectory(AnimationClip clip, GameObject previewModel, float sampleRate = 60f)
    {
        List<TrajectoryPoint> trajectory = new List<TrajectoryPoint>();
        if (clip == null || previewModel == null) return trajectory;

        float duration = clip.length;
        int totalSamples = Mathf.CeilToInt(duration * sampleRate);
        float timeStep = duration / totalSamples;

        // Lưu lại trạng thái ban đầu để khôi phục sau khi sample
        Vector3 originalPos = previewModel.transform.localPosition;
        Quaternion originalRot = previewModel.transform.localRotation;

        for (int i = 0; i <= totalSamples; i++)
        {
            float time = i * timeStep;

            // Sample animation tại thời điểm t
            clip.SampleAnimation(previewModel, time);

            // Lấy vị trí hiện tại của Root (giả sử Root là transform của previewModel)
            Vector3 currentPos = previewModel.transform.localPosition;

            trajectory.Add(new TrajectoryPoint
            {
                time = time,
                position = currentPos
            });
        }

        // Khôi phục lại trạng thái ban đầu
        previewModel.transform.localPosition = originalPos;
        previewModel.transform.localRotation = originalRot;

        return trajectory;
    }
}
#endif