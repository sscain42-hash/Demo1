using System.Collections.Generic;
using UnityEngine;

public class ComboEngine
{
    private readonly ICombatActor _actor;
    private readonly IDamageDealer _damageDealer;
    private readonly IParryCapable _parryCapable;   // null với Enemy
    private readonly Animator _animator;

    public AttackData CurrentAttackData { get; private set; }

    public Vector3 CurrentStepDisplacement { get; private set; }
    public Vector3 CurrentStepVelocity { get; private set; }

    public bool IsComboWindowActive { get; private set; }
    public bool CanDashCancelNow { get; private set; }
    public bool CanJumpCancelNow { get; private set; }

    private float _lastNormalizedTime;
    private string _lastActiveAnimation;
    private int _attackLayerIndex = 0;
    private AttackType currentAttackType;

    private readonly HashSet<ActionWindow> _triggeredWindows = new HashSet<ActionWindow>();
    private readonly HashSet<Collider> _alreadyHitTargets = new HashSet<Collider>();
    private readonly Collider[] _hitBuffer = new Collider[16];

    private readonly HashSet<ActionWindow> _hitStopTriggeredWindows = new HashSet<ActionWindow>();
    private readonly HashSet<ActionWindow> _majorVfxTriggeredWindows = new HashSet<ActionWindow>();

    private const float STOP_OFFSET = 0.2f;

    public ComboEngine(ICombatActor actor, Animator animator)
    {
        _actor = actor;
        _animator = animator;
        _damageDealer = actor as IDamageDealer;
        _parryCapable = actor as IParryCapable;

        _lastNormalizedTime = -1f;
        _lastActiveAnimation = string.Empty;
    }

    public void ChangeAttackData(AttackData newData, AttackType attackType)
    {
        CurrentAttackData = newData;
        currentAttackType = attackType;
        ResetFlags();

        _lastNormalizedTime = -1f;
        _triggeredWindows.Clear();
        _alreadyHitTargets.Clear();
        _hitStopTriggeredWindows.Clear();
        _majorVfxTriggeredWindows.Clear();

        if (CurrentAttackData == null)
        {
            CurrentStepDisplacement = Vector3.zero;
            CurrentStepVelocity = Vector3.zero;
            return;
        }

        if (_animator != null && !string.IsNullOrEmpty(CurrentAttackData.animationName))
        {
            _animator.CrossFadeInFixedTime(CurrentAttackData.animationName, 0.1f, 0, 0f);
        }
    }

    public void UpdateWindows()
    {
        if (_animator == null || CurrentAttackData == null)
        {
            CurrentStepDisplacement = Vector3.zero;
            CurrentStepVelocity = Vector3.zero;
            return;
        }

        bool isInTransition = _animator.IsInTransition(_attackLayerIndex);
        AnimatorStateInfo currentStateInfo = _animator.GetCurrentAnimatorStateInfo(_attackLayerIndex);
        AnimatorStateInfo nextStateInfo = isInTransition ? _animator.GetNextAnimatorStateInfo(_attackLayerIndex) : default;

        bool isCurrentTarget = currentStateInfo.IsName(CurrentAttackData.animationName);
        bool isNextTarget = isInTransition && nextStateInfo.IsName(CurrentAttackData.animationName);

        if (!isCurrentTarget && !isNextTarget)
        {
            ResetFlags();
            CurrentStepDisplacement = Vector3.zero;
            CurrentStepVelocity = Vector3.zero;
            _lastNormalizedTime = -1f;
            return;
        }

        AnimatorStateInfo targetStateInfo = isCurrentTarget ? currentStateInfo : nextStateInfo;
        float currentNTime = targetStateInfo.normalizedTime;

        if (_lastActiveAnimation != CurrentAttackData.animationName)
        {
            _lastNormalizedTime = -1f;
            _lastActiveAnimation = CurrentAttackData.animationName;
            _triggeredWindows.Clear();
            _alreadyHitTargets.Clear();
            _hitStopTriggeredWindows.Clear();
            _majorVfxTriggeredWindows.Clear();
        }

        if (_lastNormalizedTime < 0f) _lastNormalizedTime = currentNTime;
        if (currentNTime < _lastNormalizedTime) _lastNormalizedTime = currentNTime;

        ResetFlags();
        Vector3 accumulatedDisplacement = Vector3.zero;

        foreach (var window in CurrentAttackData.windows)
        {
            bool isInsideWindow = isCurrentTarget && window.IsInside(currentNTime);

            if (isInsideWindow)
            {
                if (window.actionName == "ComboInputBuffer") IsComboWindowActive = true;
                if (window.actionName == "DashCancel") CanDashCancelNow = true;
                if (window.actionName == "JumpCancel") CanJumpCancelNow = true;

                if (window.eventEffects != null && !_triggeredWindows.Contains(window))
                {
                    _triggeredWindows.Add(window);
                    foreach (var effect in window.eventEffects)
                        effect?.Trigger(_actor.GameObject, window);
                }

                if (window.actionName == "HitBox")
                {
                    ProcessBoxCastHitbox(window);
                }
                // 🔥 PARRY WINDOW — chỉ actor có IParryCapable mới đăng ký
                else if (window.actionName == "Parry")
                {
                    _parryCapable?.RegisterParryWindow(CurrentAttackData, window);
                }
            }
            else
            {
                if (window.actionName == "HitBox")
                {
                    if (_alreadyHitTargets.Count > 0) _alreadyHitTargets.Clear();
                    if (_hitStopTriggeredWindows.Contains(window)) _hitStopTriggeredWindows.Remove(window);
                    if (_majorVfxTriggeredWindows.Contains(window)) _majorVfxTriggeredWindows.Remove(window);
                }
            }

            // Step & Lunge Movement Processing
            if (window.actionName == "Step")
            {
                ProcessStepWindow(window, currentNTime, accumulatedDisplacement);
            }
        }

        float dt = Time.deltaTime;
        CurrentStepDisplacement = accumulatedDisplacement;
        CurrentStepVelocity = dt > 0.0001f ? (accumulatedDisplacement / dt) : Vector3.zero;

        _lastNormalizedTime = currentNTime;
    }

    private void ProcessStepWindow(ActionWindow window, float currentNTime, Vector3 accumulatedDisplacement)
    {
        float windowWidth = window.endTime - window.startTime;
        if (windowWidth <= 0.001f) return;

        float clampLast = Mathf.Clamp(_lastNormalizedTime, window.startTime, window.endTime);
        float clampCurr = Mathf.Clamp(currentNTime, window.startTime, window.endTime);
        float deltaInWindow = clampCurr - clampLast;

        if (deltaInWindow <= 0f) return;

        Camera mainCam = Camera.main;

        Vector3 fallbackDirection;
        if (window.cursorStep)
        {
            fallbackDirection = (mainCam != null) ? mainCam.transform.forward : _actor.Transform.forward;
        }
        else
        {
            fallbackDirection = GetLookDirection();
        }

        Vector3 aimDirection = fallbackDirection;

        // Soft-lock: nếu không có input, tự tìm target gần nhất
        bool hasMoveInput = HasMoveInput();
        if (!hasMoveInput && TryGetNearestTargetDirection(fallbackDirection, out Vector3 targetDir))
        {
            aimDirection = targetDir;
        }

        aimDirection.y = 0;
        aimDirection = aimDirection.sqrMagnitude > 0.0001f ? aimDirection.normalized : _actor.Transform.forward;

        // 🔥 Xoay Model thay vì transform gốc (tránh CharacterController penetration)
        if (aimDirection != Vector3.zero && _actor.Model != null)
        {
            float beforeY = _actor.Transform.position.y;
            _actor.Model.rotation = Quaternion.LookRotation(aimDirection);
            float afterY = _actor.Transform.position.y;

            if (Mathf.Abs(afterY - beforeY) > 0.001f)
            {
                Vector3 posFixed = _actor.Transform.position;
                posFixed.y = beforeY;
                _actor.Transform.position = posFixed;
            }
        }

        float totalTargetDistance = window.targetDistance.magnitude;
        float progressThisFrame = deltaInWindow / windowWidth;
        float desiredDistanceThisFrame = totalTargetDistance * progressThisFrame;

        float pRadius = 0.5f;
        Vector3 rayOrigin = _actor.Transform.position + Vector3.up * 0.5f;

        if (_actor.Transform.TryGetComponent<CharacterController>(out var controller))
        {
            pRadius = controller.radius;
            rayOrigin = _actor.Transform.position + controller.center;
        }

        LayerMask hitLayer = window.targetLayer != 0 ? window.targetLayer : Physics.DefaultRaycastLayers;

        if (Physics.SphereCast(rayOrigin, pRadius, aimDirection, out RaycastHit hit, desiredDistanceThisFrame + STOP_OFFSET, hitLayer))
        {
            float safeDistance = Mathf.Max(0f, hit.distance - pRadius - STOP_OFFSET);
            accumulatedDisplacement += aimDirection * safeDistance;
        }
        else
        {
            accumulatedDisplacement += aimDirection * desiredDistanceThisFrame;
        }
    }

    private void ProcessBoxCastHitbox(ActionWindow window)
    {
        Vector3 center = _actor.Transform.TransformPoint(window.hitBoxOffset);
        Vector3 halfExtents = window.hitBoxSize * 0.5f;
        Quaternion orientation = _actor.Transform.rotation;

        int hitCount = Physics.OverlapBoxNonAlloc(center, halfExtents, _hitBuffer, orientation, window.targetLayer);
        bool hasHitNewTarget = false;

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = _hitBuffer[i];
            if (col.gameObject == _actor.GameObject) continue;

            if (!_alreadyHitTargets.Contains(col))
            {
                _alreadyHitTargets.Add(col);
                hasHitNewTarget = true;

                if (col.TryGetComponent<Damageable>(out var damageable))
                {
                    _damageDealer?.ExecuteDamage(col.gameObject, currentAttackType);
                    Debug.Log($"[ComboEngine] {_actor.GameObject.name} hit {col.gameObject.name} with {currentAttackType}");
                }

                // VFX — chỉ spawn nếu actor có Effect
                if (_actor.Effect != null)
                {
                    Vector3 hitPosition;
                    Transform swordTransform = GetSwordTransform();
                    if (swordTransform != null)
                        hitPosition = col.ClosestPoint(swordTransform.position);
                    else
                        hitPosition = col.ClosestPoint(center);

                    Transform baseTransform = swordTransform != null ? swordTransform : _actor.Transform;
                    Vector3 currentEuler = baseTransform.rotation.eulerAngles;

                    Vector3 dirToActor = (_actor.Transform.position - hitPosition).normalized;
                    float targetZAngle = currentEuler.z;

                    if (dirToActor != Vector3.zero)
                    {
                        targetZAngle = Mathf.Atan2(dirToActor.y, dirToActor.x) * Mathf.Rad2Deg;
                    }

                    Quaternion vfxRotation = Quaternion.Euler(currentEuler.x, currentEuler.y, targetZAngle);

                    bool isPrimaryTargetInWindow = !_majorVfxTriggeredWindows.Contains(window);

                    if (isPrimaryTargetInWindow)
                    {
                        _majorVfxTriggeredWindows.Add(window);

                        _actor.Effect.SpawnVFXFromData(
                            _actor.Effect.MajorHitPrefab,
                            hitPosition,
                            vfxRotation,
                            currentAttackType
                        );
                    }
                    else if (_actor.Effect.MinorHitPrefab != null)
                    {
                        Vector3 offsetPos = hitPosition + Random.insideUnitSphere * 0.08f;

                        _actor.Effect.SpawnVFXFromData(
                            _actor.Effect.MinorHitPrefab,
                            offsetPos,
                            vfxRotation,
                            currentAttackType
                        );
                    }

                    SoundManager.Instance?.PlaySFXAtPosition(_actor.Effect.HitSFX, hitPosition);
                }
            }
        }

        if (hasHitNewTarget && !_hitStopTriggeredWindows.Contains(window))
        {
            _hitStopTriggeredWindows.Add(window);

            if (ScreenShakeManager.Instance != null && CurrentAttackData.useScreenShake)
            {
                ScreenShakeManager.Instance.TriggerShake(CurrentAttackData.shakeForce);
            }

            if (HitStopSystem.Instance != null)
            {
                HitStopSystem.Instance.Trigger(0.08f, 0.05f);
            }
        }
    }

    // 🔥 Helper — trả về null nếu actor không có sword transform
    private Transform GetSwordTransform()
    {
        if (_actor is PlayerController player) return player.SwordTransform;
        if (_actor is EnemyController enemy) return enemy.SwordTransform; // nếu Enemy có
        return null;
    }

    private bool HasMoveInput()
    {
        if (_actor is PlayerController player)
            return player.InputVector.sqrMagnitude > 0.01f;
        return false;
    }

    private Vector3 GetLookDirection()
    {
        if (_actor is PlayerController player)
            return player.GetLookDirection();
        return _actor.Transform.forward;
    }

    private bool TryGetNearestTargetDirection(Vector3 fallback, out Vector3 direction)
    {
        direction = fallback;
        if (_actor is PlayerController player && player.CombatDetection != null)
        {
            direction = player.CombatDetection.GetNearestTargetDirection(
                _actor.Transform.position,
                fallback,
                _actor.GameObject,
                flattenY: true
            );
            return true;
        }
        return false;
    }

    private void ResetFlags()
    {
        IsComboWindowActive = false;
        CanDashCancelNow = false;
        CanJumpCancelNow = false;
    }

    public float GetNormalizedTime()
    {
        if (_animator == null || CurrentAttackData == null) return 0f;

        if (_animator.IsInTransition(_attackLayerIndex))
        {
            AnimatorStateInfo nextStateInfo = _animator.GetNextAnimatorStateInfo(_attackLayerIndex);
            if (nextStateInfo.IsName(CurrentAttackData.animationName))
                return nextStateInfo.normalizedTime;
            return 0f;
        }

        AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(_attackLayerIndex);
        if (!stateInfo.IsName(CurrentAttackData.animationName)) return 0f;

        return stateInfo.normalizedTime;
    }
}