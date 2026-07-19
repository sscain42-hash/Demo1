using System.Collections.Generic; // Cần thiết để sử dụng HashSet
using UnityEngine;

public class ComboEngine
{
    private readonly GameObject _owner;
    private readonly Animator _animator;

    public AttackData CurrentAttackData { get; private set; }

    public Vector3 CurrentStepDisplacement { get; private set; }
    public Vector3 CurrentStepVelocity { get; private set; }

    public bool IsComboWindowActive { get; private set; }
    public bool CanDashCancelNow { get; private set; }
    public bool CanJumpCancelNow { get; private set; }

    private float _lastNormalizedTime;
    private string _lastActiveAnimation;

    // 🔥 GIẢI PHÁP MỚI: Dùng HashSet runtime để lưu trữ các Window đã kích hoạt, 
    // không ghi đè trực tiếp lên file Asset ScriptableObject để chống lỗi lặp x2 Event
    private readonly HashSet<ActionWindow> _triggeredWindows = new HashSet<ActionWindow>();

    public ComboEngine(GameObject owner, Animator animator, IComboCharacter character)
    {
        _owner = owner;
        _animator = animator;
        _lastNormalizedTime = -1f;
        _lastActiveAnimation = string.Empty;
    }

    public void ChangeAttackData(AttackData newData)
    {
        CurrentAttackData = newData;
        ResetFlags();

        _lastNormalizedTime = -1f;

        // 🔥 SỬA: Xóa sạch bộ nhớ tạm các sự kiện đã chạy của đòn đánh trước
        _triggeredWindows.Clear();

        if (CurrentAttackData == null)
        {
            CurrentStepDisplacement = Vector3.zero;
            CurrentStepVelocity = Vector3.zero;
            return;
        }

        // XÓA BỎ: Đoạn mã window.ResetRuntime() cũ vì nó can thiệp trực tiếp làm hỏng dữ liệu Asset cứng

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

        bool isInTransition = _animator.IsInTransition(0);
        AnimatorStateInfo currentStateInfo = _animator.GetCurrentAnimatorStateInfo(0);
        AnimatorStateInfo nextStateInfo = isInTransition ? _animator.GetNextAnimatorStateInfo(0) : default;

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
            _triggeredWindows.Clear(); // Đảm bảo dọn sạch sẽ khi đổi tên đòn
        }

        if (_lastNormalizedTime < 0f)
        {
            _lastNormalizedTime = currentNTime;
        }

        if (currentNTime < _lastNormalizedTime)
        {
            _lastNormalizedTime = currentNTime;
        }

        ResetFlags();
        Vector3 accumulatedDisplacement = Vector3.zero;

        foreach (var window in CurrentAttackData.windows)
        {
            if (isCurrentTarget && window.IsInside(currentNTime))
            {
                if (window.actionName == "ComboInputBuffer") IsComboWindowActive = true;
                if (window.actionName == "DashCancel") CanDashCancelNow = true;
                if (window.actionName == "JumpCancel") CanJumpCancelNow = true;

                // 🔥 SỬA: Chốt chặn bảo vệ tối cao dựa trên HashSet. 
                // Nếu Window này chưa nằm trong danh sách đã trigger của frame này -> Tiến hành Trigger
                if (window.eventEffects != null && !_triggeredWindows.Contains(window))
                {
                    _triggeredWindows.Add(window); // Khóa ngay lập tức
                    foreach (var effect in window.eventEffects)
                        effect?.Trigger(_owner, window);
                }
            }

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
                        Vector3 worldDirection = _owner.transform.TransformDirection(window.targetDistance.normalized);
                        float distance = window.targetDistance.magnitude;

                        float progressThisFrame = deltaInWindow / windowWidth;
                        accumulatedDisplacement += worldDirection * (distance * progressThisFrame);
                    }
                }
            }
        }

        float dt = Time.deltaTime;
        CurrentStepDisplacement = accumulatedDisplacement;
        CurrentStepVelocity = dt > 0.0001f ? (accumulatedDisplacement / dt) : Vector3.zero;

        _lastNormalizedTime = currentNTime;
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

        if (_animator.IsInTransition(0))
        {
            AnimatorStateInfo nextStateInfo = _animator.GetNextAnimatorStateInfo(0);
            if (nextStateInfo.IsName(CurrentAttackData.animationName))
                return nextStateInfo.normalizedTime;
            return 0f;
        }

        AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
        if (!stateInfo.IsName(CurrentAttackData.animationName)) return 0f;

        return stateInfo.normalizedTime;
    }
}