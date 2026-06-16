using UnityEngine;

public class HitBoxDetection : DetectionBase
{
    public enum DetectionType { Box, Sphere }

    [Header("Detector Settings")]
    public DetectionType type;
    public LayerMask mask;
    public Vector3 boxSize = Vector3.one;
    public float sphereRadius = 1f;

    private void OnEnable()
    {
        CheckCollision();    }

    private void CheckCollision()
    {
        Collider[] hits = null;

        // Quét 1 l?n duy nh?t khi Enable
        if (type == DetectionType.Box)
        {
            hits = Physics.OverlapBox(transform.position, boxSize / 2*transform.localScale.x, transform.rotation, mask);
        }
        else
        {
            hits = Physics.OverlapSphere(transform.position, sphereRadius*transform.localScale.x, mask);
        }

        // G?i k?t qu? qua UnityEvent
        if (hits != null)
        {
            foreach (var hit in hits)
            {
                CollisionEnterEvent?.Invoke(hit.gameObject);
                PositionEnterEvent?.Invoke(hit.transform.position);
            }
        }
    }

    // V? vùng quét trong Editor d? d? can ch?nh
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        if (type == DetectionType.Box)
            Gizmos.DrawWireCube(transform.position, boxSize);
        else
            Gizmos.DrawWireSphere(transform.position, sphereRadius);
    }
}