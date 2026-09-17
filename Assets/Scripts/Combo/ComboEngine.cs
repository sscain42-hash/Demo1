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

    // 🔥 Quản lý danh sách các Target đã trúng đòn trong 1 Window để tránh đòn đánh bị Multi-Hit không mong muốn
    private readonly HashSet<Collider> _alreadyHitTargets = new HashSet<Collider>();
    private readonly Collider[] _hitBuffer = new Collider[16]; // Cache cố định tránh GC Alloc

    public ComboEngine(GameObject owner, Animator animator, IComboCharacter character)
    {
        _ctx = owner.GetComponent<PlayerController>();
        _animator = animator;
        _lastNormalizedTime = -1f;
        _lastActiveAnimation = string.Empty;
    }

    public void ChangeAttackData(AttackData newData,AttackType attackType)
    {
        
        CurrentAttackData = newData;
        currentAttackType = attackType;
        ResetFlags();

        _lastNormalizedTime = -1f;
        _triggeredWindows.Clear();
        _alreadyHitTargets.Clear();

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

                // 🔥 Xử lý Event Trigger
                if (window.eventEffects != null && !_triggeredWindows.Contains(window))
                {
                    _triggeredWindows.Add(window);
                    foreach (var effect in window.eventEffects)
                        effect?.Trigger(_ctx.gameObject, window);
                }

                // 🔥 CASE MỚI: Xử lý HitBox BoxCast liên tục trong suốt Window
                if (window.actionName == "HitBox")
                {
                    ProcessBoxCastHitbox(window);
                }
            }
            else
            {
                // Khi thoát khỏi Window HitBox, clear danh sách kẻ địch trúng đòn để chuẩn bị cho đòn/window tiếp theo
                if (window.actionName == "HitBox" && _alreadyHitTargets.Count > 0)
                {
                    _alreadyHitTargets.Clear();
                }
            }

            // Xử lý Step Movement
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
                        Vector3 aimDirection = (mainCam != null) ? mainCam.transform.forward : _ctx.transform.forward;

                        if (!window.cursorStep) aimDirection.y = 0f;
                        aimDirection = aimDirection.sqrMagnitude > 0.0001f ? aimDirection.normalized : _ctx.transform.forward;

                        if (aimDirection != Vector3.zero)
                            _ctx.transform.rotation = Quaternion.LookRotation(aimDirection);

                        float distance = window.targetDistance.magnitude;
                        float progressThisFrame = deltaInWindow / windowWidth;
                        accumulatedDisplacement += aimDirection * (distance * progressThisFrame);
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
        // Tính toán vị trí World của Hitbox theo Offset
        Vector3 center = _ctx.transform.TransformPoint(window.hitBoxOffset);
        Vector3 halfExtents = window.hitBoxSize * 0.5f;
        Quaternion orientation = _ctx.transform.rotation;

        int hitCount = Physics.OverlapBoxNonAlloc(center, halfExtents, _hitBuffer, orientation, window.targetLayer);
        Gizmos.DrawCube(center, window.hitBoxSize); // Vẽ Gizmo để debug HitBox
        for (int i = 0; i < hitCount; i++)
        {
            Collider col = _hitBuffer[i];
            if (col.gameObject == _ctx) continue;

            // Kiểm tra xem Target đã bị trúng đòn trong đợt quét này chưa
            if (!_alreadyHitTargets.Contains(col))
            {
                _alreadyHitTargets.Add(col);

                // Gửi thông báo trúng đòn đến Target (ví dụ gọi Interface IDamageable)
                if (col.TryGetComponent<Damageable>(out var damageable))
                {
                   
                  _ctx.ExecuteDamage(col.gameObject,currentAttackType );
                }

                Debug.Log($"Trúng đòn: {col.name}");
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