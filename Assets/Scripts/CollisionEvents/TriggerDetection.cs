using UnityEngine;

public class TriggerDetection : DetectionBase
{
 
    
    private void OnTriggerEnter(Collider other)
    {
        if(!layerToCheck.Contains(other.gameObject)) return;
        Debug.Log($"Trigger Enter: {other.gameObject.name}");
        CollisionEnterEvent?.Invoke(other.gameObject);
        PositionEnterEvent?.Invoke(other.ClosestPointOnBounds(transform.position));
    }

    private void OnTriggerExit(Collider other)
    {
        if(!layerToCheck.Contains(other.gameObject)) return;
        
        CollisionExitEvent?.Invoke(other.gameObject);
        PositionExitEvent?.Invoke(other.ClosestPointOnBounds(transform.position));
    }
}

