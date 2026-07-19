using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class KnockbackHandler : MonoBehaviour, IKnockbackable
{
    private NavMeshAgent _agent;
    private bool _isKnockingBack;

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
    }

    public void OnKnockback(Vector3 direction, float force, AnimationCurve curve)
    {
        if (_isKnockingBack) StopAllCoroutines();
        StartCoroutine(KnockbackRoutine(direction, force, curve));
    }

    private IEnumerator KnockbackRoutine(Vector3 direction, float force, AnimationCurve curve)
    {
        _isKnockingBack = true;

        if (_agent != null && _agent.enabled) _agent.enabled = false;

        float duration = 0.25f;
        float elapsed = 0f;
        Vector3 startPos = transform.position;
        Vector3 targetPos = startPos + (direction * force);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Dùng curve truyền từ bên ngoài vào
            float curveValue = curve.Evaluate(t);
            transform.position = Vector3.Lerp(startPos, targetPos, curveValue);

            yield return null;
        }

        if (_agent != null)
        {
            _agent.enabled = true;
            _agent.Warp(transform.position);
        }
        _isKnockingBack = false;
    }
}