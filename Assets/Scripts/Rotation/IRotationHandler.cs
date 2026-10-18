using UnityEngine;

/// <summary>
/// Single Responsibility: Handle player model rotation
/// </summary>
public interface IRotationHandler
{
    void RotateTowardCamera(Transform model, Transform camera, float rotationSpeed);
    void RotateTowardDirection(Transform model, Vector3 direction, float rotationSpeed);
}
