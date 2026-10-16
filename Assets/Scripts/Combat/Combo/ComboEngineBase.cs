using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Shared logic cho mọi actor có combo.
/// </summary>
public abstract class ComboEngineBase
{
    protected readonly ICombatActor Actor;
    protected readonly IDamageDealer DamageDealer;
    protected readonly Animator Animator;

    public AttackData CurrentAttackData { get; private set; }
    public Vector3 CurrentStepDisplacement { get; private set; }
    public Vector3 CurrentStepVelocity { get; private set; }

    public bool IsComboWindowActive { get; private set; }
    public bool CanDashCancelNow { get; private set; }
    public bool CanJumpCancelNow { get; private set; }

    protected AttackType CurrentAttackType { get; private set; }

    public event System.Action<GameObject> OnParriedByTarget;

    private float _lastNormalizedTime;
    private const int AttackLayerIndex = 0;
    private const float StopOffset = 0.2f;

    // 🔥 SESSION TRACKING
    private int _currentSessionId = 0;
    private int _observedSessionId = -1;
    private float _sessionStartTime = 0f;
    private const float SESSION_GRACE_TIME = 0.08f;  // 80ms

    // 🔥 MINIMUM PLAY TIME — không bao giờ kết thúc trước X giây
    private const float MIN_ANIMATION_DURATION = 0.1f;

    private readonly HashSet<ActionWindow> _triggeredWindows = new HashSet<ActionWindow>();
    private readonly HashSet<Collider> _alreadyHitTargets = new HashSet<Collider>();
    private readonly Collider[] _hitBuffer = new Collider[16];
    private readonly HashSet<ActionWindow> _hitStopTriggeredWindows = new HashSet<ActionWindow>();
    private readonly HashSet<ActionWindow> _majorVfxTriggeredWindows = new HashSet<ActionWindow>();

    // 🔥 CACHE HASH — nhanh hơn string compare
    private int _currentAnimationHash = 0;

    protected ComboEngineBase(ICombatActor actor, Animator animator)
    {
        if (actor == null)
            Debug.LogError($"[ComboEngineBase] actor null khi khởi tạo {GetType().Name}");

        Actor = actor;
        Animator = animator;
        DamageDealer = actor as IDamageDealer;
        _lastNormalizedTime = -1f;
    }

    // =========================================================
    // HOOKS
    // =========================================================
    protected abstract Vector3 ResolveAimDirection(ActionWindow window, Vector3 fallback);
    protected virtual bool SupportsParry => false;
    protected virtual bool SupportsDashCancel => false;
    protected virtual bool SupportsJumpCancel => false;
    protected virtual void ApplyRotationForStep(Vector3 aimDirection) { }
    protected virtual bool TryGetStepOverrideDirection(ActionWindow window, out Vector3 direction)
    {
        direction = Vector3.zero;
        return false;
    }
    protected virtual void OnTelegraphStart() { }
    protected virtual void OnTelegraphEnd() { }

    // =========================================================
    // PUBLIC API
    // =========================================================

    public void ChangeAttackData(AttackData newData, AttackType attackType)
    {
        CurrentAttackData = newData;
        CurrentAttackType = attackType;
        ResetFlags();

        _lastNormalizedTime = -1f;
        _triggeredWindows.Clear();
        _alreadyHitTargets.Clear();
        _hitStopTriggeredWindows.Clear();
        _majorVfxTriggeredWindows.Clear();

        // 🔥 SESSION MỚI
        _currentSessionId++;
        _sessionStartTime = Time.time;

        if (CurrentAttackData == null)
        {
            CurrentStepDisplacement = Vector3.zero;
            CurrentStepVelocity = Vector3.zero;
            return;
        }

        if (Animator != null && !string.IsNullOrEmpty(CurrentAttackData.animationName))
        {
            ForcePlayAnimation(CurrentAttackData.animationName);
        }
    }

    /// <summary>
    /// 🔥 FORCE RESTART ANIMATION — đảm bảo animation chạy lại từ frame 0.
    /// 
    /// Cách hoạt động:
    /// 1. Kiểm tra Animator có state với tên này không
    /// 2. Gọi Play() để force restart từ đầu
    /// 3. Set normalizedTime = 0
    /// </summary>
    private void ForcePlayAnimation(string animationName)
    {
        if (Animator == null) return;
        if (string.IsNullOrEmpty(animationName)) return;

        // 🔥 CÁCH 1: Dùng Animator.Play() để force restart
        // Play(stateName, layer, normalizedTime)
        try
        {
            Animator.Play(animationName, AttackLayerIndex, 0f);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[ComboEngine] Play animation '{animationName}' thất bại: {e.Message}");
        }

        // 🔥 CÁCH 2: CrossFade để có blend mượt
        // Nhưng phải ForceUpdate trước để đảm bảo state đã reset
        Animator.Update(0f);
        Animator.CrossFadeInFixedTime(animationName, 0.1f, AttackLayerIndex, 0f);

        Debug.Log($"[ComboEngine] ForcePlayAnimation: '{animationName}' session #{_currentSessionId}");
    }

    public void UpdateWindows()
    {
        if (Animator == null || CurrentAttackData == null)
        {
            CurrentStepDisplacement = Vector3.zero;
            CurrentStepVelocity = Vector3.zero;
            return;
        }

        bool isInTransition = Animator.IsInTransition(AttackLayerIndex);
        AnimatorStateInfo currentStateInfo = Animator.GetCurrentAnimatorStateInfo(AttackLayerIndex);
        AnimatorStateInfo nextStateInfo = isInTransition
            ? Animator.GetNextAnimatorStateInfo(AttackLayerIndex)
            : default;

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

        // 🔥 RESET SESSION KHI LẦN ĐẦU OBSERVE
        if (_observedSessionId != _currentSessionId)
        {
            _observedSessionId = _currentSessionId;
            _lastNormalizedTime = currentNTime;
            _triggeredWindows.Clear();
            _alreadyHitTargets.Clear();
            _hitStopTriggeredWindows.Clear();
            _majorVfxTriggeredWindows.Clear();

            Debug.Log($"[ComboEngine] Session observed #{_currentSessionId}, nTime={currentNTime:F2}");
        }

        if (_lastNormalizedTime < 0f) _lastNormalizedTime = currentNTime;
        if (currentNTime < _lastNormalizedTime) _lastNormalizedTime = currentNTime;

        ResetFlags();
        Vector3 accumulatedDisplacement = Vector3.zero;

        foreach (var window in CurrentAttackData.windows)
        {
            bool isInsideWindow = isCurrentTarget && window.IsInside(currentNTime);

            if (isInsideWindow)
                ProcessWindowActive(window);
            else
                ProcessWindowInactive(window);

            if (window.actionName == "Step")
                accumulatedDisplacement += ProcessStepWindow(window, currentNTime);
        }

        float dt = Time.deltaTime;
        CurrentStepDisplacement = accumulatedDisplacement;
        CurrentStepVelocity = dt > 0.0001f ? (accumulatedDisplacement / dt) : Vector3.zero;

        _lastNormalizedTime = currentNTime;
    }

    // =========================================================
    // 🔥 FIXED: IsAnimationFinished — đáng tin cậy 100%
    // =========================================================

    public bool IsAnimationFinished()
    {
        if (Animator == null || CurrentAttackData == null) return false;

        // 1. Grace period — không kết thúc trong 80ms đầu
        float timeSinceStart = Time.time - _sessionStartTime;
        if (timeSinceStart < SESSION_GRACE_TIME) return false;

        // 2. Minimum duration — không kết thúc trước 100ms
        if (timeSinceStart < MIN_ANIMATION_DURATION) return false;

        // 3. Đang transition — chưa xong
        if (Animator.IsInTransition(AttackLayerIndex)) return false;

        // 4. Session chưa observe — chưa xong
        if (_observedSessionId != _currentSessionId) return false;

        // 5. Check state
        AnimatorStateInfo stateInfo = Animator.GetCurrentAnimatorStateInfo(AttackLayerIndex);

        if (!stateInfo.IsName(CurrentAttackData.animationName))
            return false;

        // 🔥 6. Check normalizedTime >= 0.98 (không phải 1.0 vì có thể bị kẹt)
        float nTime = stateInfo.normalizedTime;

        // 7. Nếu nTime < 0.5 → animation mới bắt đầu lại → CHƯA xong
        if (nTime < 0.5f) return false;

        // 8. Chỉ xong khi nTime >= 0.98
        return nTime >= 0.98f;
    }

    public float GetNormalizedTime()
    {
        if (Animator == null || CurrentAttackData == null) return -1f;

        if (Animator.IsInTransition(AttackLayerIndex))
        {
            AnimatorStateInfo nextStateInfo = Animator.GetNextAnimatorStateInfo(AttackLayerIndex);
            if (nextStateInfo.IsName(CurrentAttackData.animationName))
                return 0f;
            return -1f;
        }

        AnimatorStateInfo stateInfo = Animator.GetCurrentAnimatorStateInfo(AttackLayerIndex);
        if (!stateInfo.IsName(CurrentAttackData.animationName)) return -1f;
        if (_observedSessionId != _currentSessionId) return -1f;

        return stateInfo.normalizedTime;
    }

    // =========================================================
    // WINDOW PROCESSING
    // =========================================================

    private void ProcessWindowActive(ActionWindow window)
    {
        switch (window.actionName)
        {
            case "ComboInputBuffer": IsComboWindowActive = true; break;
            case "DashCancel": if (SupportsDashCancel) CanDashCancelNow = true; break;
            case "JumpCancel": if (SupportsJumpCancel) CanJumpCancelNow = true; break;
            case "Telegraph": OnTelegraphStart(); break;
        }

        if (window.eventEffects != null && !_triggeredWindows.Contains(window))
        {
            _triggeredWindows.Add(window);
            foreach (var effect in window.eventEffects)
                effect?.Trigger(Actor.GameObject, window);
        }

        if (window.actionName == "HitBox")
            ProcessBoxCastHitbox(window);
    }

    private void ProcessWindowInactive(ActionWindow window)
    {
        if (window.actionName == "Telegraph")
        {
            OnTelegraphEnd();
            return;
        }

        if (window.actionName != "HitBox") return;

        if (_alreadyHitTargets.Count > 0) _alreadyHitTargets.Clear();
        if (_hitStopTriggeredWindows.Contains(window)) _hitStopTriggeredWindows.Remove(window);
        if (_majorVfxTriggeredWindows.Contains(window)) _majorVfxTriggeredWindows.Remove(window);
    }

    // =========================================================
    // STEP
    // =========================================================

    private Vector3 ProcessStepWindow(ActionWindow window, float currentNTime)
    {
        float windowWidth = window.endTime - window.startTime;
        if (windowWidth <= 0.001f) return Vector3.zero;

        float clampLast = Mathf.Clamp(_lastNormalizedTime, window.startTime, window.endTime);
        float clampCurr = Mathf.Clamp(currentNTime, window.startTime, window.endTime);
        float deltaInWindow = clampCurr - clampLast;

        if (deltaInWindow <= 0f) return Vector3.zero;

        Vector3 fallback = window.cursorStep ? GetCameraForward() : GetDefaultForward();

        Vector3 aimDirection;
        if (!TryGetStepOverrideDirection(window, out aimDirection))
            aimDirection = ResolveAimDirection(window, fallback);

        aimDirection.y = 0;
        aimDirection = aimDirection.sqrMagnitude > 0.0001f
            ? aimDirection.normalized
            : Actor.Transform.forward;

        ApplyRotationForStep(aimDirection);

        float totalDistance = window.targetDistance.magnitude;
        float progressThisFrame = deltaInWindow / windowWidth;
        float desiredDistanceThisFrame = totalDistance * progressThisFrame;

        return ComputeSafeDisplacement(aimDirection, desiredDistanceThisFrame, window.targetLayer);
    }

    private Vector3 ComputeSafeDisplacement(Vector3 direction, float desiredDistance, LayerMask targetLayer)
    {
        float pRadius = 0.5f;
        Vector3 rayOrigin = Actor.Transform.position + Vector3.up * 0.5f;

        if (Actor.Transform.TryGetComponent<CharacterController>(out var controller))
        {
            pRadius = controller.radius;
            rayOrigin = Actor.Transform.position + controller.center;
        }

        LayerMask hitLayer = targetLayer != 0 ? targetLayer : Physics.DefaultRaycastLayers;

        if (Physics.SphereCast(rayOrigin, pRadius, direction, out RaycastHit hit,
                desiredDistance + StopOffset, hitLayer))
        {
            float safeDistance = Mathf.Max(0f, hit.distance - pRadius - StopOffset);
            return direction * safeDistance;
        }

        return direction * desiredDistance;
    }

    // =========================================================
    // HITBOX
    // =========================================================

    private void ProcessBoxCastHitbox(ActionWindow window)
    {
        Vector3 center = Actor.Transform.TransformPoint(window.hitBoxOffset);
        Vector3 halfExtents = window.hitBoxSize * 0.5f;
        Quaternion orientation = Actor.Transform.rotation;

        int hitCount = Physics.OverlapBoxNonAlloc(center, halfExtents, _hitBuffer, orientation, window.targetLayer);
        bool hasHitNewTarget = false;

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = _hitBuffer[i];
            if (col.gameObject == Actor.GameObject) continue;
            if (_alreadyHitTargets.Contains(col)) continue;

            _alreadyHitTargets.Add(col);
            hasHitNewTarget = true;

            var parryReceiver = col.GetComponent<IParryReceiver>();
            if (parryReceiver != null && parryReceiver.TryParry(Actor.GameObject))
            {
                OnParriedByTarget?.Invoke(col.gameObject);
                continue;
            }

            if (col.TryGetComponent<Damageable>(out _))
                DamageDealer?.ExecuteDamage(col.gameObject, CurrentAttackType);

            SpawnHitVfx(col, window, center);
        }

        if (hasHitNewTarget && !_hitStopTriggeredWindows.Contains(window))
        {
            _hitStopTriggeredWindows.Add(window);
            TriggerHitStopAndShake();
        }
    }

    private void SpawnHitVfx(Collider col, ActionWindow window, Vector3 center)
    {
        if (Actor.Effect == null) return;

        Transform swordTransform = GetSwordTransform();
        Vector3 hitPosition = swordTransform != null
            ? col.ClosestPoint(swordTransform.position)
            : col.ClosestPoint(center);

        Transform baseTransform = swordTransform != null ? swordTransform : Actor.Transform;
        Vector3 currentEuler = baseTransform.rotation.eulerAngles;

        Vector3 dirToActor = (Actor.Transform.position - hitPosition).normalized;
        float targetZAngle = currentEuler.z;
        if (dirToActor != Vector3.zero)
            targetZAngle = Mathf.Atan2(dirToActor.y, dirToActor.x) * Mathf.Rad2Deg;

        Quaternion vfxRotation = Quaternion.Euler(currentEuler.x, currentEuler.y, targetZAngle);

        bool isPrimary = !_majorVfxTriggeredWindows.Contains(window);
        if (isPrimary)
        {
            _majorVfxTriggeredWindows.Add(window);
            Actor.Effect.SpawnVFXFromData(Actor.Effect.MajorHitPrefab, hitPosition, vfxRotation, CurrentAttackType);
        }
        else if (Actor.Effect.MinorHitPrefab != null)
        {
            Vector3 offsetPos = hitPosition + Random.insideUnitSphere * 0.08f;
            Actor.Effect.SpawnVFXFromData(Actor.Effect.MinorHitPrefab, offsetPos, vfxRotation, CurrentAttackType);
        }

        if (Actor.Effect.HitSFX != null)
            SoundManager.Instance?.PlaySFXAtPosition(Actor.Effect.HitSFX, hitPosition);
    }

    private void TriggerHitStopAndShake()
    {
        if (ScreenShakeManager.Instance != null && CurrentAttackData.useScreenShake)
            ScreenShakeManager.Instance.TriggerShake(CurrentAttackData.shakeForce);

        if (HitStopSystem.Instance != null)
            HitStopSystem.Instance.Trigger(0.08f, 0.05f);
    }

    // =========================================================
    // UTILITIES
    // =========================================================

    protected virtual Transform GetSwordTransform() => null;

    private Vector3 GetDefaultForward()
    {
        if (Actor == null || Actor.Transform == null) return Vector3.forward;
        return Actor.Transform.forward;
    }

    private Vector3 GetCameraForward()
    {
        Camera mainCam = Camera.main;
        if (mainCam != null) return mainCam.transform.forward;
        return GetDefaultForward();
    }

    private void ResetFlags()
    {
        IsComboWindowActive = false;
        CanDashCancelNow = false;
        CanJumpCancelNow = false;
    }
}