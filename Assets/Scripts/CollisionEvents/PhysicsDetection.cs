using System;
using System.Collections.Generic;
using System.Linq;
using NaughtyAttributes;
using ParadoxNotion;
using UnityEngine;
using UnityEngine.Rendering;

public class PhysicsDetection : DetectionBase, IPooled<PhysicsDetection>
{
    [Tooltip("Bán kính kiểm tra va chạm"), Range(0.1f, 20f)]
    public float radiusCheck;

    [Tooltip("Layer cần kiểm tra va chạm")]
    public LayerMask layerToCheck;

    [SerializeField] private bool drawGizmos;
    [SerializeField] private Color color ;
 
    private readonly Collider[] hitColliders = new Collider[10];
    public List<GameObject>  GetTargets()
    {
        return new List<GameObject>(hitColliders.Where(c => c != null).Select(c => c.gameObject));
    }
    public void CheckCollision()
    {
        var numCol = Physics.OverlapSphereNonAlloc(transform.position, radiusCheck, hitColliders, layerToCheck);
        for (var i = 0; i < numCol; i++)
        {
            CollisionEnterEvent?.Invoke(hitColliders[i].gameObject);
            PositionEnterEvent?.Invoke(hitColliders[i].ClosestPointOnBounds(transform.position));
        }
    }

   
    public void Release() => ReleaseCallback?.Invoke(this);
    public Action<PhysicsDetection> ReleaseCallback { get; set; }

    void OnDrawGizmos()
    {
        if (!drawGizmos) return;
        Gizmos.color = color.WithAlpha(1f);
        GizmoUtils.DrawCircle(transform.position, radiusCheck);
     
    }
}