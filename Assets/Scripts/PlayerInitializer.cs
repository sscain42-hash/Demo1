using UnityEngine;

public class PlayerInitializer : MonoBehaviour
{
    public TargetLockManager lockManager;
    public CameraFocusController cameraController;

    private void Awake()
    {
        // Kết nối hai module độc lập qua Event
        cameraController.Setup(lockManager);
    }
}