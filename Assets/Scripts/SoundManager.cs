using UnityEngine;
using System.Collections.Generic;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("--- Settings ---")]
    [Range(0f, 1f)][SerializeField] private float masterVolume = 1f;
    [Range(0f, 1f)][SerializeField] private float sfxVolume = 1f;
    [Range(0f, 1f)][SerializeField] private float musicVolume = 1f;

    [Header("--- Audio Sources ---")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private int initialSfxPoolSize = 10;

    // Pool quản lý các AudioSource dùng riêng cho SFX
    private List<AudioSource> _sfxPool = new List<AudioSource>();

    private void Awake()
    {
        // Khởi tạo Singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializePool();
    }

    private void InitializePool()
    {
        for (int i = 0; i < initialSfxPoolSize; i++)
        {
            AudioSource source = CreateNewAudioSource($"SFX_Source_{i}");
            _sfxPool.Add(source);
        }
    }

    private AudioSource CreateNewAudioSource(string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform);
        AudioSource source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        return source;
    }

    private AudioSource GetAvailableSFXSource()
    {
        foreach (var source in _sfxPool)
        {
            if (!source.isPlaying)
            {
                return source;
            }
        }

        AudioSource newSource = CreateNewAudioSource($"SFX_Source_{_sfxPool.Count}");
        _sfxPool.Add(newSource);
        return newSource;
    }

    #region SFX Methods

    /// <summary>
    /// Phát âm thanh SFX 2D với biến thiên ngẫu nhiên Pitch và Volume
    /// </summary>
    /// <param name="clip">Clip âm thanh</param>
    /// <param name="minPitch">Pitch tối thiểu (mặc định 0.9f)</param>
    /// <param name="maxPitch">Pitch tối đa (mặc định 1.1f)</param>
    /// <param name="volumeVariance">Độ dao động volume ngẫu nhiên (mặc định 0.1f)</param>
    /// <param name="baseVolumeScale">Tỷ lệ volume gốc</param>
    public void PlaySFX(
        AudioClip clip,
        float minPitch = 0.9f,
        float maxPitch = 1.1f,
        float volumeVariance = 0.1f,
        float baseVolumeScale = 1f)
    {
        if (clip == null) return;

        AudioSource source = GetAvailableSFXSource();
        source.spatialBlend = 0f; // 2D Sound

        // 1. Random Pitch
        source.pitch = Random.Range(minPitch, maxPitch);

        // 2. Random Volume (dao động nhẹ quanh giá trị base)
        float randomVolOffset = Random.Range(-volumeVariance, volumeVariance);
        float finalVolumeScale = Mathf.Clamp01(baseVolumeScale + randomVolOffset);
        source.volume = sfxVolume * masterVolume * finalVolumeScale;

        source.clip = clip;
        source.Play();
    }

    /// <summary>
    /// Phát âm thanh SFX 3D tại một vị trí cụ thể với biến thiên Pitch và Volume
    /// </summary>
    public void PlaySFXAtPosition(
        AudioClip clip,
        Vector3 position,
        float minPitch = 0.9f,
        float maxPitch = 1.1f,
        float volumeVariance = 0.1f,
        float baseVolumeScale = 1f)
    {
        if (clip == null) return;

        AudioSource source = GetAvailableSFXSource();
        source.transform.position = position;
        source.spatialBlend = 1f; // 3D Sound
        source.minDistance = 1f;
        source.maxDistance = 20f;

        // 1. Random Pitch
        source.pitch = Random.Range(minPitch, maxPitch);

        // 2. Random Volume
        float randomVolOffset = Random.Range(-volumeVariance, volumeVariance);
        float finalVolumeScale = Mathf.Clamp01(baseVolumeScale + randomVolOffset);
        source.volume = sfxVolume * masterVolume * finalVolumeScale;

        source.clip = clip;
        source.Play();
    }

    /// <summary>
    /// [Bổ sung] Chọn ngẫu nhiên 1 clip trong danh sách và phát kèm biến thiên Pitch/Volume
    /// (Rất hữu ích cho Round-Robin/Multi-sampling tiếng chém, tiếng va chạm)
    /// </summary>
    public void PlayRandomSFX(
        List<AudioClip> clips,
        Vector3? position = null,
        float minPitch = 0.9f,
        float maxPitch = 1.1f,
        float volumeVariance = 0.1f,
        float baseVolumeScale = 1f)
    {
        if (clips == null || clips.Count == 0) return;

        int randomIndex = Random.Range(0, clips.Count);
        AudioClip selectedClip = clips[randomIndex];

        if (position.HasValue)
        {
            PlaySFXAtPosition(selectedClip, position.Value, minPitch, maxPitch, volumeVariance, baseVolumeScale);
        }
        else
        {
            PlaySFX(selectedClip, minPitch, maxPitch, volumeVariance, baseVolumeScale);
        }
    }

    #endregion

    #region Music Methods

    public void PlayMusic(AudioClip musicClip, bool loop = true)
    {
        if (musicClip == null || musicSource == null) return;

        musicSource.clip = musicClip;
        musicSource.loop = loop;
        musicSource.volume = musicVolume * masterVolume;
        musicSource.pitch = 1f; // Luôn reset pitch nhạc về chuẩn
        musicSource.Play();
    }

    public void StopMusic()
    {
        if (musicSource != null)
        {
            musicSource.Stop();
        }
    }

    #endregion

    #region Volume Controls

    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        UpdateVolumes();
    }

    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        UpdateVolumes();
    }

    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        UpdateVolumes();
    }

    private void UpdateVolumes()
    {
        if (musicSource != null)
        {
            musicSource.volume = musicVolume * masterVolume;
        }
    }

    #endregion
}