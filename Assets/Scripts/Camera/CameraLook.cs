using UnityEngine;

public class CameraLook : MonoBehaviour
{
    [Header("Camera Control")]
    [SerializeField] private Transform _cameraTarget;
    [SerializeField] private float _sensitivity = 0.4f;
    [SerializeField] private PlayerInputs _playerInputs;
    private float _xRotation;
    private float _yRotation;

    private void Start()
    {
        
    }

    private void LateUpdate()
    {
        UpdateRotation();
    }

    private void UpdateRotation()
    {
        float mouseX = _playerInputs.Look.x * _sensitivity;
        float mouseY = _playerInputs.Look.y * _sensitivity;

        _yRotation += mouseX;
        _xRotation += mouseY;

        if (_cameraTarget != null)
            _cameraTarget.rotation = Quaternion.Euler(-_xRotation, _yRotation, 0);

        transform.rotation = Quaternion.Euler(0, _yRotation, 0);
    }
}