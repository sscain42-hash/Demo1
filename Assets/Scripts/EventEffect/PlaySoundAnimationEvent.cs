using UnityEngine;

[CreateAssetMenu(fileName = "PlaySoundAnimationEvent", menuName = "Animation Events/Play Sound")]
public class PlaySoundAnimationEvent : AnimationEvent
{
    [Header("Audio Bank Settings")]
    [Tooltip("Danh sách các âm thanh (tiếng chém 1, tiếng chém 2,...). Hệ thống sẽ chọn ngẫu nhiên 1 âm thanh.")]
    [SerializeField] private AudioClip[] _audioClips;

    [Header("Audio Parameters")]
    [SerializeField, Range(0f, 1f)] private float _volume = 1f;
    [SerializeField, Range(0.1f, 3f)] private float _pitchMin = 0.9f;
    [SerializeField, Range(0.1f, 3f)] private float _pitchMax = 1.1f;

    [Header("3D Sound Settings")]
    [SerializeField] private bool _playAtCasterPosition = true;

    public override void Trigger(GameObject caster, ActionWindow sourceWindow = null)
    {
        // 1. Kiểm tra an toàn
        if (_audioClips == null || _audioClips.Length == 0 || caster == null) return;

        // 2. Lấy ngẫu nhiên 1 AudioClip từ Bank
        AudioClip selectedClip = _audioClips[Random.Range(0, _audioClips.Length)];
        if (selectedClip == null) return;

        // 3. Tính Pitch ngẫu nhiên để âm thanh tự nhiên, không bị trùng lặp
        float randomPitch = Random.Range(_pitchMin, _pitchMax);

        // 4. Xử lý phát âm thanh
        if (_playAtCasterPosition)
        {
            // Phát âm thanh 3D tại vị trí của Caster
            AudioSource.PlayClipAtPoint(selectedClip, caster.transform.position, _volume);
        }
        else
        {
            // Phát qua AudioSource gắn trên Caster
            if (caster.TryGetComponent<AudioSource>(out var audioSource))
            {
                audioSource.pitch = randomPitch;
                audioSource.PlayOneShot(selectedClip, _volume);
            }
            else
            {
                // Fallback nếu Caster không có AudioSource
                AudioSource.PlayClipAtPoint(selectedClip, caster.transform.position, _volume);
            }
        }
    }
}