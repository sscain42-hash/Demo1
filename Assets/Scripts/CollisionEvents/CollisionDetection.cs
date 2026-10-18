using UnityEngine;

public class CollisionDetection : DetectionBase
{
    private void OnCollisionEnter(Collision other)
    {
        if (!layerToCheck.Contains(other.gameObject))
            return;

        CollisionEnterEvent?.Invoke(other.gameObject);

        // 🔥 Guard: Check contact count trước khi GetContact
        if (other.contactCount > 0)
        {
            PositionEnterEvent?.Invoke(other.GetContact(0).point);
        }
        else
        {
            // Fallback: dùng vị trí của object
            PositionEnterEvent?.Invoke(other.transform.position);
        }
    }

    private void OnCollisionExit(Collision other)
    {
        if (!layerToCheck.Contains(other.gameObject))
            return;

        CollisionExitEvent?.Invoke(other.gameObject);

        // 🔥 FIX: OnCollisionExit KHÔNG có contact point
        // → Dùng vị trí của object thay vì GetContact
        PositionExitEvent?.Invoke(other.transform.position);

        // HOẶC nếu không cần position:
        // PositionExitEvent?.Invoke(Vector3.zero);
    }
}
