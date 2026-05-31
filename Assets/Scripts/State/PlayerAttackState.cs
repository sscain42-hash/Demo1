using System.Collections.Generic;
using UnityEngine;

public class PlayerAttackState : PlayerBaseState
{
    private ComboSequence _normalAttackCombo;

    private int _currentIndex = 0;

    private float _bufferTimer = 0f;

    private const float BUFFER_WINDOW = 0.2f;

    private HashSet<string> _activeWindows = new();

    // ================= ATTACK MOVE =================

    private float _attackMoveDistance = 1.2f;

    private float _attackMoveDuration = 0.15f;

    private float _attackMoveTimer = 0f;

    private AnimationCurve _attackMoveCurve =
        new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.1f, 1.5f),
            new Keyframe(0.6f, 0.8f),
            new Keyframe(1f, 0f)
        );

    // ================= CONSTRUCTOR =================

    public PlayerAttackState(
        PlayerController ctx,
        PlayerStateFactory factory
    ) : base(ctx, factory)
    {
        _normalAttackCombo = ctx._normalAttackCombo;

        _isRootState = true;
    }

    // ================= ENTER =================

    public override void EnterState()
    {
        _currentIndex = 0;

        StartAttackStep();

    }

    // ================= START STEP =================

    private void StartAttackStep()
    {
        _activeWindows.Clear();

        _bufferTimer = 0f;

        _attackMoveTimer =
            _attackMoveDuration;

        _ctx.SetAttackLock(true);

        _ctx.SetRotationLock(true);

        // 👉 tránh leo collider quái
        _ctx.CharController.stepOffset = 0f;

        var data =
            _normalAttackCombo.attacks[_currentIndex];

        _ctx.PlayAnimation(
            data.animationName,
            0.05f
        );
    }

    // ================= UPDATE =================

    protected override void UpdateState()
    {
        var data =
            _normalAttackCombo.attacks[_currentIndex];

        var stateInfo =
            _ctx.Animator.GetCurrentAnimatorStateInfo(0);

        float nTime =
            Mathf.Clamp01(
                stateInfo.normalizedTime
            );

        // ================= BUFFER =================

        if (_bufferTimer > 0f)
        {
            _bufferTimer -= Time.deltaTime;
        }

        if (_ctx._playerInputs.HasCommand(BufferedAction.NormalAttack))
        {
            _bufferTimer =
                BUFFER_WINDOW;
        }

        // ================= MOVE =================

        ApplyAttackMovement();

        // ================= WINDOWS =================

        foreach (var window in data.windows)
        {
            bool inside =
                window.IsInside(nTime);

            // ENTER
            if (inside &&
                !_activeWindows.Contains(
                    window.actionName
                ))
            {
                _activeWindows.Add(
                    window.actionName
                );

                OnWindowEnter(
                    window.actionName
                );
            }

            // STAY
            if (inside)
            {
                OnWindowStay(
                    window.actionName
                );
            }

            // EXIT
            if (!inside &&
                _activeWindows.Contains(
                    window.actionName
                ))
            {
                _activeWindows.Remove(
                    window.actionName
                );

                OnWindowExit(
                    window.actionName
                );
            }
        }

        // ================= END =================

        if (nTime >= 0.98f)
        {
            if (_bufferTimer > 0f)
            {
                ExecuteCombo();
            }
            else
            {
                CheckSwitchState();
            }
        }
    }

    // ================= ATTACK MOVE =================

    private void ApplyAttackMovement()
    {
        if (_attackMoveTimer <= 0f)
            return;

        Vector2 input =
            _ctx.InputVector;

        bool hasInput =
            input.sqrMagnitude > 0.01f;

        Vector3 moveDir = Vector3.zero;

        // ================= INPUT MOVE =================

        if (hasInput)
        {
            // 👉 hủy auto lunge nếu player điều khiển
            _ctx.StopLunge();

            moveDir =
                _ctx.GetLookDirection();

            if (moveDir.sqrMagnitude < 0.001f)
                return;

            moveDir =
                Vector3.ProjectOnPlane(
                    moveDir,
                    _ctx.GroundNormal
                ).normalized;

            // 👉 xoay theo input
            Quaternion targetRot =
                Quaternion.LookRotation(moveDir);

            _ctx.Model.rotation =
                Quaternion.Slerp(
                    _ctx.Model.rotation,
                    targetRot,
                    _ctx.RotationSpeed *
                    Time.deltaTime
                );
        }
       

        // ================= CURVE =================

        float normalizedTime =
            1f -
            (
                _attackMoveTimer /
                _attackMoveDuration
            );

        float curveValue =
            _attackMoveCurve.Evaluate(
                normalizedTime
            );

        float baseSpeed =
            _attackMoveDistance /
            _attackMoveDuration;

        float speed =
            baseSpeed *
            curveValue;

        // ================= MOVE =================

        MoveStep(moveDir, speed);
    }

   

    private void MoveStep(Vector3 moveDir, float speed)
    {
        Vector3 delta =
            moveDir *
            speed *
            Time.deltaTime;

        _ctx.CharController.Move(delta);

        _attackMoveTimer -=
            Time.deltaTime;
    }

    // ================= WINDOWS =================

    private void OnWindowEnter(string action)
    {
        switch (action)
        {
            case "Hitbox":


                _ctx.CharacterEffect.CheckNACollision();


                break;


        }
    }

    private void OnWindowStay(string action)
    {
        switch (action)
        {
            case "ComboInput":

                if (_ctx.TryNormalAttack)
                {
                    ExecuteCombo();
                }

                break;

            case "DashCancel":

                if (_ctx.TryDash)
                {
                    SwitchState(
                        _factory.Dash()
                    );
                }

                break;

            case "JumpCancel":

                if (_ctx.JumpBufferCounter > 0)
                {
                    SwitchState(
                        _factory.Jumping()
                    );
                }

                break;
        }
    }

    private void OnWindowExit(string action)
    {
        if (action == "Hitbox")
        {
            _ctx._weaponHitbox.SetActive(
                false
            );
        }
    }

    // ================= COMBO =================

    private void ExecuteCombo()
    {
        _ctx._playerInputs.ConsumeCommand(
         BufferedAction.NormalAttack);

        if (_currentIndex <
            _normalAttackCombo.attacks.Count - 1)
        {
            _currentIndex++;
        }
        else
        {
            _currentIndex = 0;
        }

        StartAttackStep();
    }

    // ================= SWITCH =================

    public override void CheckSwitchState()
    {
        if (_ctx.CharController.isGrounded)
        {
            SwitchState(
                _factory.Grounded()
            );
        }
        else
        {
            SwitchState(
                _factory.Falling()
            );
        }
    }

    // ================= EXIT =================

    protected override void ExitState()
    {
        _activeWindows.Clear();

        _ctx.SetAttackLock(false);

        _ctx.SetRotationLock(false);

        // 👉 trả lại step offset
        _ctx.CharController.stepOffset =
            0.3f;
    }
}