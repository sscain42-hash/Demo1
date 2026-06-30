using UnityEngine;

public class ComboEngine
{
    private readonly GameObject _owner;
    private readonly Animator _animator;

    public AttackData CurrentAttackData { get; private set; }
    public Vector3 CurrentStepVelocity { get; private set; }
    public bool IsComboWindowActive { get; private set; }
    public bool CanDashCancelNow { get; private set; }
    public bool CanJumpCancelNow { get; private set; }

    private GameObject _trackedLungeTarget;

    public ComboEngine(GameObject owner, Animator animator, IComboCharacter character)
    {
        _owner = owner;
        _animator = animator;
    }

    public void ChangeAttackData(AttackData newData)
    {
        CurrentAttackData = newData;
        CurrentStepVelocity = Vector3.zero;
        _trackedLungeTarget = null;
        ResetFlags();

        // 🌟 NẾU LÀ NULL (BỊ FORCE CANCEL): Dừng hoàn toàn, không gọi Animator nữa để tránh kẹt đè hoạt ảnh cũ
        if (CurrentAttackData == null) return;

        if (CurrentAttackData.windows != null)
        {
            foreach (var window in CurrentAttackData.windows)
                window.ResetRuntime();
        }

        // Kiểm tra xem đòn đánh mới có chứa cấu hình Lunge hay không
        bool hasLungeWindow = false;
        foreach (var window in CurrentAttackData.windows)
        {
            if (window.enableLunge) { hasLungeWindow = true; break; }
        }

        var playerCtrl = _owner.GetComponent<PlayerController>();
        if (hasLungeWindow && playerCtrl != null && playerCtrl.InputVector.sqrMagnitude <= 0.01f)
        {
            _trackedLungeTarget = playerCtrl.ScanAndGetClosestLungeTarget();
        }

        if (_animator != null)
        {
            _animator.CrossFadeInFixedTime(CurrentAttackData.animationName, 0.1f, 0, 0f);
        }
    }

    public void UpdateWindows()
    {
        // Nếu không có dữ liệu đòn đánh, lập tức triệt tiêu vận tốc và thoát
        if (_animator == null || CurrentAttackData == null)
        {
            CurrentStepVelocity = Vector3.zero;
            return;
        }

        AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
        if (!stateInfo.IsName(CurrentAttackData.animationName))
        {
            ResetFlags();
            CurrentStepVelocity = Vector3.zero;
            return;
        }

        float nTime = stateInfo.normalizedTime;
        float effectiveLength = stateInfo.length / Mathf.Max(stateInfo.speed, 0.001f);

        ResetFlags();
        Vector3 accumulatedVelocity = Vector3.zero;

        foreach (var window in CurrentAttackData.windows)
        {
            if (window.IsInside(nTime))
            {
                // 1. SMART LUNGE (Giữ nguyên logic hoạt động tốt của bạn)
                if (window.enableLunge)
                {
                    if (!window.isLungeInitialized)
                    {
                        window.lungeDirection = _owner.transform.forward;

                        if (_trackedLungeTarget != null)
                        {
                            window.calculatedTargetPos = _trackedLungeTarget.transform.position;
                            Vector3 currentPos = _owner.transform.position;
                            Vector3 targetPos = window.calculatedTargetPos;
                            targetPos.y = currentPos.y;

                            float distance = Vector3.Distance(currentPos, targetPos);
                            window.actualLungeDistanceLeft = Mathf.Min(Mathf.Max(distance - window.keepDistanceOffset, 0f), window.maxLungeDistance);
                        }
                        else
                        {
                            window.actualLungeDistanceLeft = 0f;
                            window.calculatedTargetPos = _owner.transform.position;
                        }

                        window.isLungeInitialized = true;
                    }

                    if (window.actualLungeDistanceLeft > 0f)
                    {
                        Vector3 currentPos = _owner.transform.position;
                        Vector3 targetPos = window.calculatedTargetPos;
                        Vector3 dir = (targetPos - currentPos).normalized;

                        Vector3 desiredPos = targetPos - dir * window.keepDistanceOffset;
                        desiredPos.y = currentPos.y;

                        Vector3 nextPos = Vector3.MoveTowards(currentPos, desiredPos, window.lungeSpeed * Time.deltaTime);
                        Vector3 deltaMovement = nextPos - currentPos;

                        if (Time.deltaTime > 0f) accumulatedVelocity = deltaMovement / Time.deltaTime;
                        _owner.transform.rotation = Quaternion.LookRotation(dir);
                    }
                }
                // 2. MOVEMENT STEP (🔥 SỬA TẠI ĐÂY: Làm mượt lực lướt tiêu hao dần)
                else if (!window.enableLunge && window.targetDistance != Vector3.zero)
                {
                    if (!window.isLungeInitialized)
                    {
                        // Khởi tạo hành trình ban đầu
                        window.actualLungeDistanceLeft = window.targetDistance.magnitude;
                        window.lungeDirection = _owner.transform.TransformDirection(window.targetDistance.normalized);
                        window.isLungeInitialized = true;
                    }

                    if (window.actualLungeDistanceLeft > 0f)
                    {
                        float duration = Mathf.Max((window.endTime - window.startTime) * effectiveLength, 0.001f);

                        // Vận tốc cơ sở ban đầu
                        float baseSpeed = window.targetDistance.magnitude / duration;

                        // Tính toán tỷ lệ phần trăm thời gian đã trôi qua trong ô Window này để giảm tốc (Decay)
                        float progress = (nTime - window.startTime) / (window.endTime - window.startTime);
                        progress = Mathf.Clamp01(progress);

                        // Tốc độ mượt mà giảm dần về 0 theo thời gian đòn đánh trôi qua
                        float smoothSpeed = Mathf.Lerp(baseSpeed * 1.5f, 0f, progress);

                        // Trừ dần hành trình thực tế tránh bị bay quá xa
                        window.actualLungeDistanceLeft -= smoothSpeed * Time.deltaTime;

                        accumulatedVelocity += window.lungeDirection * smoothSpeed;
                    }
                }

                if (window.actionName == "ComboInputBuffer") IsComboWindowActive = true;
                if (window.actionName == "DashCancel") CanDashCancelNow = true;
                if (window.actionName == "JumpCancel") CanJumpCancelNow = true;

                if (window.eventEffects != null && !window.eventTriggered)
                {
                    window.eventTriggered = true;
                    foreach (var effect in window.eventEffects) effect?.Trigger(_owner, window);
                }
            }
            else
            {
                if (window.isLungeInitialized) window.isLungeInitialized = false;
            }
        }

        CurrentStepVelocity = accumulatedVelocity;
    }

    private void ResetFlags()
    {
        IsComboWindowActive = false;
        CanDashCancelNow = false;
        CanJumpCancelNow = false;
    }

    public float GetNormalizedTime()
    {
        if (_animator == null) return 0f;
        AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
        return stateInfo.normalizedTime;
    }
}