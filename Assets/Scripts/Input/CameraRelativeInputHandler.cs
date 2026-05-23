using UnityEngine;

/// <summary>
/// Read input and compute camera-relative movement direction
/// </summary>
public class CameraRelativeInputHandler : IInputHandler
{
    private readonly Transform _camera;
    private Vector2 _lastInput;


    public CameraRelativeInputHandler(Transform camera)
    {
        _camera = camera;
    }

    public Vector2 ReadMovementInput()
    {
        _lastInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
      
        return _lastInput;
    }

    public Vector3 GetMovementDirection(Vector2 input)
    {
        if (_camera == null) return new Vector3(input.x, 0, input.y).normalized;

        Vector3 forward = _camera.forward;
        Vector3 right = _camera.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();

        right.Normalize ();

        return (forward * input.y + right * input.x).normalized;
    }
}