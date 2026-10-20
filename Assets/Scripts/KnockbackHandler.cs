using System.Collections;
using UnityEngine;

public class KnockbackHandler : MonoBehaviour, IKnockbackable
{
    private UnityEngine.AI.NavMeshAgent _agent;
    private CharacterController _cc;
    private bool _isKnockingBack;
    private Coroutine _knockbackCoroutine;

    void Awake()
    {
        _agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        _cc = GetComponent<CharacterController>();
    }

    public void OnKnockback(Vector3 direction, float force, AnimationCurve curve)
    {
        // 🔥 Nếu đang parry → cancel knockback
        var pc = GetComponent<PlayerController>();
        if (pc != null && (pc.CurrentState is PlayerParryState || pc.CurrentState is PlayerParryMovementState))
        {
            Debug.Log("[Knockback] Cancelled — player đang parry");
            return;
        }

        if (_knockbackCoroutine != null) StopCoroutine(_knockbackCoroutine);
        _knockbackCoroutine = StartCoroutine(KnockbackRoutine(direction, force, curve));
    }

    private IEnumerator KnockbackRoutine(Vector3 direction, float force, AnimationCurve curve)
    {
        _isKnockingBack = true;
        if (_agent != null && _agent.enabled) _agent.enabled = false;

        float duration = 0.25f;
        float elapsed = 0f;
        Vector3 startPos = transform.position;
        Vector3 targetPos = startPos + (direction * force);
        Vector3 prevPos = startPos;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float curveValue = curve.Evaluate(t);
            Vector3 newPos = Vector3.Lerp(startPos, targetPos, curveValue);
            Vector3 delta = newPos - prevPos;
            prevPos = newPos;

            // 🔥 Qua CharacterController để exclusive provider / parry có thể chặn
            if (_cc != null && _cc.enabled) _cc.Move(delta);
            else transform.position = newPos;

            yield return null;
        }

        if (_agent != null && _agent.enabled == false)
        {
            _agent.enabled = true;
            _agent.Warp(transform.position);
        }
        _isKnockingBack = false;
    }
}