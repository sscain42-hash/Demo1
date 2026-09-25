using UnityEngine;

public class PlayerLungeState : PlayerBaseState
{
    private Transform _targetTransform;
    private Vector3 _startPosition;
    private Vector3 _lungeDirection;
    private float _totalLungeDistance;
    private float _timer;
    private float _duration;

    private readonly Collider[] _hitBuffer = new Collider[16];
    private const float STOP_OFFSET = 0.2f;

    public PlayerLungeState(PlayerController ctx, PlayerStateFactory factory)
        : base(ctx, factory)
    {
        _isRootState = true;
    }

    public override void EnterState()
    {
        _ctx.SetRotationLock(true);
        _timer = 0f;

        // 1. Quét tìm Target trong bán kính lungeRange
        FindLungeTarget();

        // 2. Nếu không tìm thấy target hợp lệ, chuyển ngay sang Attack hoặc Grounded
        if (_targetTransform == null || _totalLungeDistance <= 0.05f)
        {
            CheckSwitchState();
            return;
        }

        // 3. Tính toán thời gian lunge dựa theo quãng đường và tốc độ
        // Độ dài thời gian lunge = Quãng đường / Tốc độ (Sử dụng lungeSpd từ PlayerController)
        float speed = Mathf.Max(0.1f, _ctx.RunMaxSpeed * 1.5f); // Hoặc dùng biến lungeSpd nếu có public
        _duration = Mathf.Clamp(_totalLungeDistance / speed, 0.1f, 0.35f);

        // Quay mặt về phía target
        if (_lungeDirection.sqrMagnitude > 0.001f)
        {
            _ctx.Model.rotation = Quaternion.LookRotation(_lungeDirection);
        }
    }

    private void FindLungeTarget()
    {
        _targetTransform = null;
        _startPosition = _ctx.transform.position;

        Vector3 lookDir = _ctx.GetLookDirection();
        lookDir.y = 0;
        if (lookDir.sqrMagnitude < 0.001f) lookDir = _ctx.Model.forward;

        int count = Physics.OverlapSphereNonAlloc(_startPosition, _ctx.lungeRange, _hitBuffer);
        float closestDist = float.MaxValue;
        Collider closestCol = null;

        for (int i = 0; i < count; i++)
        {
            Collider col = _hitBuffer[i];
            if (col.gameObject == _ctx.gameObject) continue;

            if (col.GetComponent<Damageable>() != null)
            {
                Vector3 dirToCol = (col.transform.position - _startPosition);
                dirToCol.y = 0;

                // Angle Check: Chỉ lunge vào mục tiêu nằm trong góc quạt 60 độ phía trước
                if (Vector3.Angle(lookDir, dirToCol.normalized) <= 60f)
                {
                    float dist = dirToCol.magnitude;
                    if (dist <= _ctx.lungeRange && dist < closestDist)
                    {
                        closestDist = dist;
                        closestCol = col;
                    }
                }
            }
        }

        if (closestCol != null)
        {
            _targetTransform = closestCol.transform;
            Vector3 dirToTarget = (_targetTransform.position - _startPosition);
            dirToTarget.y = 0;

            _lungeDirection = dirToTarget.normalized;
            _totalLungeDistance = Mathf.Max(0f, dirToTarget.magnitude - _ctx.offsetLunge);
        }
    }

    protected override void UpdateState()
    {
        if (_targetTransform == null || _totalLungeDistance <= 0.05f)
        {
            CheckSwitchState();
            return;
        }

        _timer += Time.deltaTime;
        float progress = Mathf.Clamp01(_timer / _duration);

        // Tính toán khoảng cách di chuyển theo Curve
        float evaluatedProgress = _ctx.DashCurve.Evaluate(progress);
        Vector3 targetPos = _startPosition + _lungeDirection * (_totalLungeDistance * evaluatedProgress);
        Vector3 moveDisplacement = targetPos - _ctx.transform.position;
        moveDisplacement.y = 0f;

        // Xử lý va chạm SphereCast tránh đâm xuyên tường/kẻ địch
        float pRadius = _ctx.CharController.radius;
        Vector3 rayOrigin = _ctx.transform.position + _ctx.CharController.center;

        if (Physics.SphereCast(rayOrigin, pRadius, _lungeDirection, out RaycastHit hit, moveDisplacement.magnitude + STOP_OFFSET))
        {
            if (hit.transform != _targetTransform)
            {
                float safeDist = Mathf.Max(0f, hit.distance - pRadius - STOP_OFFSET);
                moveDisplacement = _lungeDirection * safeDist;
            }
        }

        _ctx.CharController.Move(moveDisplacement);

        if (_timer >= _duration)
        {
            CheckSwitchState();
        }
    }

    protected override void ExitState()
    {
        _ctx.SetRotationLock(false);
        _ctx.Velocity = Vector3.zero;
    }

    public override void CheckSwitchState()
    {
        // Khi lunge kết thúc, ưu tiên chuyển sang AttackState để thực hiện đòn chém
        if (_ctx.TryNormalAttack || _ctx.IsAttacking)
        {
            SwitchState(_factory.Attack());
            return;
        }

        if (_ctx.CharController.isGrounded)
            SwitchState(_factory.Grounded());
        else
            SwitchState(_factory.Falling());
    }
}