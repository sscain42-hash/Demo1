using NaughtyAttributes;
using Unity.Cinemachine;
using UnityEngine;

[RequireComponent(typeof(CinemachineImpulseSource))]
public class ScreenShakeManager : MonoBehaviour
{
    public static ScreenShakeManager Instance { get; private set; }
    private CinemachineImpulseSource _impulseSource;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        _impulseSource = GetComponent<CinemachineImpulseSource>();
    }

    public void TriggerShake(float force)
    {
        if (_impulseSource == null) return;

        // Phát xung lực rung theo mọi hướng dựa trên cường độ lực truyền vào
        _impulseSource.GenerateImpulseWithVelocity(Random.insideUnitSphere.normalized * force);
    }
    [Button]
    public void TriggerShake()
    {
        TriggerShake(2f); // Mặc định cường độ rung là 2 nếu không truyền vào
    }
}
