using NodeCanvas.Tasks.Actions;
using System.Collections.Generic;
using UnityEngine;

public class ComboEngine
{
    private readonly PlayerController _ctx;
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

    // Đánh dấu Window đã kích hoạt hiệu ứng toàn cục (HitStop / ScreenShake / Major VFX)
    private readonly HashSet<ActionWindow> _hitStopTriggeredWindows = new HashSet<ActionWindow>();
    private readonly HashSet<ActionWindow> _majorVfxTriggeredWindows = new HashSet<ActionWindow>();

    private const float STOP_OFFSET = 0.2f;

    public ComboEngine(GameObject owner, Animator animator, IComboCharacter character)
    {
        _ctx = owner.GetComponent<PlayerController>();
        _animator = animator;
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
                        effect?.Trigger(_ctx.gameObject, window);
                }

                if (window.actionName == "HitBox")
                {
                    ProcessBoxCastHitbox(window);
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

            // Step Movement Processing...
            if (window.actionName == "Step")
            {
                float windowWidth = window.endTime - window.startTime;
                if (windowWidth > 0.001f)
                {
                    float clampLast = Mathf.Clamp(_lastNormalizedTime, window.startTime, window.endTime);
                    float clampCurr = Mathf.Clamp(currentNTime, window.startTime, window.endTime);
                    float deltaInWindow = clampCurr - clampLast;

                    if (deltaInWindow > 0f)
                    {
                        Camera mainCam = Camera.main;
                        Vector3 aimDirection = Vector3.zero;
                        if (window.cursorStep)
                        {
                            aimDirection = (mainCam != null) ? mainCam.transform.forward : _ctx.transform.forward;
                        }
                        else
                        {
                            aimDirection = _ctx.GetLookDirection();
                        }

                        aimDirection.y = 0;
                        aimDirection = aimDirection.sqrMagnitude > 0.0001f ? aimDirection.normalized : _ctx.transform.forward;

                        if (aimDirection != Vector3.zero)
                            _ctx.transform.rotation = Quaternion.LookRotation(aimDirection);

                        float totalTargetDistance = window.targetDistance.magnitude;
                        float progressThisFrame = deltaInWindow / windowWidth;
                        float desiredDistanceThisFrame = totalTargetDistance * progressThisFrame;

                        float pRadius = 0.5f;
                        Vector3 rayOrigin = _ctx.transform.position + Vector3.up * 0.5f;

                        if (_ctx.TryGetComponent<CharacterController>(out var controller))
                        {
                            pRadius = controller.radius;
                            rayOrigin = _ctx.transform.position + controller.center;
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
                }
            }
        }

        float dt = Time.deltaTime;
        CurrentStepDisplacement = accumulatedDisplacement;
        CurrentStepVelocity = dt > 0.0001f ? (accumulatedDisplacement / dt) : Vector3.zero;

        _lastNormalizedTime = currentNTime;
    }

    private void ProcessBoxCastHitbox(ActionWindow window)
    {
        // 1. Quét Hitbox bằng BoxCast rộng để gây Damage
        Vector3 center = _ctx.transform.TransformPoint(window.hitBoxOffset);
        Vector3 halfExtents = window.hitBoxSize * 0.5f;
        Quaternion orientation = _ctx.transform.rotation;

        int hitCount = Physics.OverlapBoxNonAlloc(center, halfExtents, _hitBuffer, orientation, window.targetLayer);
        bool hasHitNewTarget = false;

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = _hitBuffer[i];
            if (col.gameObject == _ctx.gameObject) continue;

            if (!_alreadyHitTargets.Contains(col))
            {
                _alreadyHitTargets.Add(col);
                hasHitNewTarget = true;

                // 1. Gây Damage
                if (col.TryGetComponent<Damageable>(out var damageable))
                {
                    _ctx.ExecuteDamage(col.gameObject, currentAttackType);
                }

                // 2. Tính toán điểm va chạm
                Vector3 hitPosition;
                if (_ctx.SwordTransform != null)
                {
                    hitPosition = col.ClosestPoint(_ctx.SwordTransform.position);
                }
                else
                {
                    hitPosition = col.ClosestPoint(center);
                }

                // 3. TÍNH TOÁN ROTATION VFX (Chỉ thay đổi trục Z)
                // Lấy góc euler hiện tại từ SwordTransform (hoặc Player nếu không có SwordTransform)
                Transform baseTransform = _ctx.SwordTransform != null ? _ctx.SwordTransform : _ctx.transform;
                Vector3 currentEuler = baseTransform.rotation.eulerAngles;

                // Tính góc Z hướng về phía Player
                Vector3 dirToPlayer = (_ctx.transform.position - hitPosition).normalized;
                float targetZAngle = currentEuler.z;

                if (dirToPlayer != Vector3.zero)
                {
                    // Tính góc nghiêng (Z) dựa trên hướng từ vị trí trúng đòn về phía Player
                    targetZAngle = Mathf.Atan2(dirToPlayer.y, dirToPlayer.x) * Mathf.Rad2Deg;
                }

                // Tạo Quaternion mới: Giữ nguyên X, Y từ thanh kiếm/Player, CHỈ THAY ĐỔI TRỤC Z
                Quaternion vfxRotation = Quaternion.Euler(currentEuler.x, currentEuler.y, targetZAngle);

                // 4. Phân loại Major VFX vs Minor VFX
                bool isPrimaryTargetInWindow = !_majorVfxTriggeredWindows.Contains(window);

                if (isPrimaryTargetInWindow)
                {
                    _majorVfxTriggeredWindows.Add(window);

                    _ctx.CharacterEffect.SpawnVFXFromData(
                        _ctx.CharacterEffect.MajorHitPrefab,
                        hitPosition,
                        vfxRotation,
                        currentAttackType
                    );
                }
                else
                {
                    Vector3 offsetPos = hitPosition + Random.insideUnitSphere * 0.08f;

                    _ctx.CharacterEffect.SpawnVFXFromData(
                        _ctx.CharacterEffect.MinorHitPrefab,
                        offsetPos,
                        vfxRotation,
                        currentAttackType
                    );
                }
                SoundManager.Instance?.PlaySFXAtPosition(_ctx.CharacterEffect.HitSFX, hitPosition);
                Debug.Log($"Trúng đòn: {col.name} | Primary Target: {isPrimaryTargetInWindow}");
            }
        }

        // 5. Kích hoạt HitStop & ScreenShake
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
