using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "SlashAnimationEvent", menuName = "Combat/Events/Slash Event")]
public class SlashAnimationEvent : AnimationEvent
{
    [Header("Slash Configuration")]
    public SlashDataSO slashData;

    [Header("Duration & Timing Options")]
    [Tooltip("Nếu bật: Tự động tính khoảng thời gian (giây) của ActionWindow để vệt chém chạy trọn vẹn từ 0% -> 100%.")]
    public bool useFixedDuration = true;

    [Tooltip("Thời gian mờ dần và hủy VFX sau khi kết thúc Window")]
    public float fadeOutTime = 0.15f;

    public override void Trigger(GameObject owner, ActionWindow window)
    {
        if (slashData == null || owner == null) return;

        PlayerController player = owner.GetComponent<PlayerController>();
        if (player == null) return;

        // 1. Xác định vị trí xuất hiện VFX
        Transform spawnTransform = player.SwordTransform != null ? player.SwordTransform : player.transform;

        GameObject slashObj = new GameObject($"SlashVFX_{window.actionName}");
        slashObj.transform.SetPositionAndRotation(spawnTransform.position, spawnTransform.rotation);

        SlashMeshGenerator generator = slashObj.AddComponent<SlashMeshGenerator>();
        generator.SetData(slashData);

        // 2. Chạy Coroutine cập nhật vệt chém
        if (useFixedDuration)
        {
            player.StartCoroutine(RoutineUpdateSlashFixedTime(player, generator, window));
        }
        else
        {
            player.StartCoroutine(RoutineUpdateSlashNormalized(player, generator, window));
        }
    }

    /// <summary>
    /// Chạy trọn vẹn Progress (0.0 -> 1.0) trong đúng số giây quy đổi từ ActionWindow
    /// </summary>
    private IEnumerator RoutineUpdateSlashFixedTime(PlayerController player, SlashMeshGenerator generator, ActionWindow window)
    {
        Animator animator = player.GetComponentInChildren<Animator>();
        if (animator == null)
        {
            Destroy(generator.gameObject);
            yield break;
        }

        // Lấy thông tin Clip và Tốc độ phát hiện tại của Animator
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        float clipLength = stateInfo.length;
        float animSpeed = Mathf.Max(0.001f, animator.speed * stateInfo.speed);

        // Quy đổi độ dài ActionWindow (0.0 - 1.0) ra thời gian thực tế (Giây)
        float windowNormalizedDuration = Mathf.Max(0.0001f, window.endTime - window.startTime);
        float targetDuration = (windowNormalizedDuration * clipLength) / animSpeed;

        float elapsedTime = 0f;

        // Chạy trọn vẹn progress từ 0.0 -> 1.0 theo Time.deltaTime
        while (elapsedTime < targetDuration && generator != null)
        {
            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedTime / targetDuration);

            generator.UpdateProgress(progress);
            yield return null;
        }

        // Mờ dần và dọn dẹp VFX
        if (generator != null)
        {
            generator.StartFadeAndDestroy(fadeOutTime);
        }
    }

    /// <summary>
    /// Bám sát trực tiếp theo Normalized Time của Animator theo từng Frame
    /// </summary>
    private IEnumerator RoutineUpdateSlashNormalized(PlayerController player, SlashMeshGenerator generator, ActionWindow window)
    {
        Animator animator = player.GetComponentInChildren<Animator>();
        if (animator == null)
        {
            Destroy(generator.gameObject);
            yield break;
        }

        float windowDuration = window.endTime - window.startTime;
        if (windowDuration <= 0.0001f) yield break;

        while (generator != null)
        {
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            float currentNTime = Mathf.Repeat(info.normalizedTime, 1f);

            // Nếu vượt khỏi Window thì dừng
            if (currentNTime > window.endTime || currentNTime < window.startTime)
            {
                generator.StartFadeAndDestroy(fadeOutTime);
                yield break;
            }

            float progress = Mathf.Clamp01((currentNTime - window.startTime) / windowDuration);
            generator.UpdateProgress(progress);

            yield return null;
        }
    }
}