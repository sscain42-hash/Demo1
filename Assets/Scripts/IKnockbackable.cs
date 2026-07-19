using UnityEngine;

public interface IKnockbackable
{
    void OnKnockback(Vector3 direction, float force, AnimationCurve curve);
}
