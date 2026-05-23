using UnityEngine;

/// <summary>
/// Single Responsibility: Handle input reading and camera-relative movement
/// </summary>
public interface IInputHandler
{
    Vector2 ReadMovementInput();
    Vector3 GetMovementDirection(Vector2 input);

}