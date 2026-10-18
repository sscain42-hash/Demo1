using UnityEngine;

/// <summary>
/// Encapsulate rotation logic
/// </summary>
public class ModelRotationHandler : IRotationHandler
{
    public void RotateTowardCamera(Transform model, Transform camera, float rotationSpeed)
    {
        if (model == null || camera == null) return;

        Vector3 cameraForward = camera.forward;
        cameraForward.y = 0f;

        if (cameraForward.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(cameraForward);
            model.rotation = Quaternion.Slerp(model.rotation, targetRotation, rotationSpeed * 2f * Time.deltaTime);
        }
    }

    public void RotateTowardDirection(Transform model, Vector3 direction, float rotationSpeed)
    {
        if (model == null || direction.sqrMagnitude < 0.01f) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        model.rotation = Quaternion.Slerp(model.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }
}
