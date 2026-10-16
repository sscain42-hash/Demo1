using System.Collections;
using UnityEngine;

namespace Combat.Parry
{
    /// <summary>
    /// Enemy đơn giản để test parry.
    /// 
    /// SETUP:
    /// 1. Tạo GameObject có Collider (Is Trigger)
    /// 2. Gắn script này
    /// 3. Gán Player Transform
    /// 4. Cấu hình telegraphDuration, attackInterval, damage
    /// </summary>
    public class ParryTestEnemy : MonoBehaviour
    {
        [Header("=== TARGET ===")]
        [SerializeField] private Transform target;

        [Header("=== ATTACK TIMING ===")]
        [Tooltip("Thời gian telegraph trước khi hitbox active (giây)")]
        [SerializeField] private float telegraphDuration = 0.8f;

        [Tooltip("Thời gian hitbox active (giây)")]
        [SerializeField] private float hitboxActiveDuration = 0.15f;

        [Tooltip("Thời gian giữa các đòn đánh (giây)")]
        [SerializeField] private float attackInterval = 2f;

        [Header("=== ATTACK PROPERTIES ===")]
        [SerializeField] private int damageAmount = 20;
        [SerializeField] private float attackRange = 2f;
        [SerializeField] private float hitboxRadius = 1f;

        [Header("=== VISUAL (optional) ===")]
        [SerializeField] private Renderer telegraphRenderer;
        [SerializeField] private Color telegraphColor = Color.yellow;
        [SerializeField] private Color idleColor = Color.gray;
        [SerializeField] private Color attackColor = Color.red;

        [Header("=== DEBUG ===")]
        [SerializeField] private bool debugLog = true;

        private ParryController _playerParry;
        private bool _isTelegraphing;
        private bool _isAttacking;

        private void Start()
        {
            if (target == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) target = player.transform;
            }

            if (target != null)
                _playerParry = target.GetComponent<ParryController>();

            StartCoroutine(AttackLoop());
        }

        private void Update()
        {
            if (telegraphRenderer != null)
            {
                if (_isAttacking)
                    telegraphRenderer.material.color = attackColor;
                else if (_isTelegraphing)
                    telegraphRenderer.material.color = telegraphColor;
                else
                    telegraphRenderer.material.color = idleColor;
            }
        }

        private IEnumerator AttackLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(attackInterval);

                if (target == null) continue;

                float distance = Vector3.Distance(transform.position, target.position);
                if (distance > attackRange)
                {
                    if (debugLog)
                        Debug.Log($"[TestEnemy] Player quá xa ({distance:F1}m > {attackRange}m) — bỏ qua đòn");
                    continue;
                }

                yield return StartCoroutine(AttackRoutine());
            }
        }

        private IEnumerator AttackRoutine()
        {
            // 1. Telegraph
            _isTelegraphing = true;
            if (_playerParry != null)
                _playerParry.SetTelegraphing(true);

            if (debugLog)
                Debug.Log($"[TestEnemy] TELEGRAPH START — {telegraphDuration}s");

            yield return new WaitForSeconds(telegraphDuration);

            // 2. Attack
            _isTelegraphing = false;
            _isAttacking = true;

            if (_playerParry != null)
                _playerParry.SetTelegraphing(false);

            if (debugLog)
                Debug.Log($"[TestEnemy] ATTACK!");

            // 3. Hitbox active — check parry
            yield return StartCoroutine(HitboxActiveRoutine());

            _isAttacking = false;
        }

        private IEnumerator HitboxActiveRoutine()
        {
            float elapsed = 0f;
            bool hasHit = false;

            while (elapsed < hitboxActiveDuration)
            {
                elapsed += Time.deltaTime;

                if (!hasHit && target != null)
                {
                    float distance = Vector3.Distance(transform.position, target.position);
                    if (distance <= hitboxRadius)
                    {
                        hasHit = true;

                        // CHECK PARRY
                        var parryReceiver = target.GetComponent<IParryReceiver>();
                        if (parryReceiver != null && parryReceiver.TryParry(gameObject))
                        {
                            if (debugLog)
                                Debug.Log($"[TestEnemy] Player PARRY thành công!");
                        }
                        else
                        {
                            // Gây damage
                            if (DamageableData.Contains(target.gameObject, out var receiver))
                            {
                                receiver.TakeDMG(damageAmount, false);
                                if (debugLog)
                                    Debug.Log($"[TestEnemy] Player nhận {damageAmount} damage");
                            }
                        }
                    }
                }

                yield return null;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, hitboxRadius);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}