using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
[RequireComponent(typeof(Reference))]
public class HitEffectParticleHandler : MonoBehaviour
{
    private ParticleSystem _particleSystem;
    private Reference _reference;

    private void Awake()
    {
        _particleSystem = GetComponent<ParticleSystem>();
        _reference = GetComponent<Reference>();
    }

    // Hàm Callback ?n c?a Unity, t? d?ng ch?y ngay khi Particle System d?ng phát h?t
    private void OnParticleSystemStopped()
    {
        // G?i hàm gi?i phóng c?a chính script Reference b?n dã vi?t d? tr? v? Pool
        _reference.Release();
    }
}