using System.Collections.Generic;
using UnityEngine;

public class PlayerAttackState : PlayerBaseState
{
    private ComboSequence _normalAttackCombo;
    private int _currentIndex = 0;

    private float _bufferTimer = 0f;
    private const float BUFFER_WINDOW = 0.2f;

    private HashSet<string> _activeWindows = new();

    public PlayerAttackState(PlayerController ctx, PlayerStateFactory factory)
        : base(ctx, factory)
    {
        _normalAttackCombo = ctx._normalAttackCombo;
        _isRootState = true;
    }

    public override void EnterState()
    {
        _currentIndex = 0;
        StartAttackStep();
        _ctx. LungeToTarget();
    }

    private void StartAttackStep()
    {
        _activeWindows.Clear();
        _bufferTimer = 0f;

        _ctx.SetAttackLock(true);
        _ctx.SetRotationLock(true);

        var data = _normalAttackCombo.attacks[_currentIndex];
        _ctx.PlayAnimation(data.animationName, 0.05f);
    }

    protected override void UpdateState()
    {
        var data = _normalAttackCombo.attacks[_currentIndex];
        var stateInfo = _ctx.Animator.GetCurrentAnimatorStateInfo(0);
        float nTime = stateInfo.normalizedTime % 1f;

        // INPUT BUFFER
        if (_bufferTimer > 0)
            _bufferTimer -= Time.deltaTime;

        if (Input.GetMouseButtonDown(0))
            _bufferTimer = BUFFER_WINDOW;

        foreach (var window in data.windows)
        {
            bool inside = window.IsInside(nTime);

            // ENTER
            if (inside && !_activeWindows.Contains(window.actionName))
            {
                _activeWindows.Add(window.actionName);
                OnWindowEnter(window.actionName);
            }

            // STAY (không còn xử lý Hitbox ở đây)
            if (inside)
            {
                OnWindowStay(window.actionName);
            }

            // EXIT
            if (!inside && _activeWindows.Contains(window.actionName))
            {
                _activeWindows.Remove(window.actionName);
                OnWindowExit(window.actionName);
            }
        }

        // END ANIMATION
        if (nTime >= 0.98f)
        {
            if (_bufferTimer > 0)
            {
                ExecuteCombo();
            }
            else
            {
                CheckSwitchState();
            }
        }
    }

    // ================= WINDOW EVENTS =================

    private void OnWindowEnter(string action)
    {
        switch (action)
        {
            case "Hitbox":

                _ctx._weaponHitbox.SetActive(true);

                // 🔥 CHỈ CHECK 1 LẦN DUY NHẤT
                var targets = _ctx.GetDetectedTargets();

                HashSet<int> uniqueTargets = new();

                foreach (var t in targets)
                {
                    if (t == null) continue;

                    var root = t.transform.root.gameObject;
                    int id = root.GetInstanceID();

                    if (uniqueTargets.Contains(id)) continue;
                    uniqueTargets.Add(id);

                    _ctx.CauseDMG(root, AttackType.NormalAttack);
                }

                break;

            case "HitBox":
                _ctx.CheckNADetection();
                break;
        }
    }

    private void OnWindowStay(string action)
    {
        switch (action)
        {
            case "ComboInput":

                if (_bufferTimer > 0)
                {
                    ExecuteCombo();
                }

                break;

            case "DashCancel":

                if (_ctx.CanDash)
                {
                    SwitchState(_factory.Dash());
                }

                break;

            case "JumpCancel":

                if (_ctx.JumpBufferCounter > 0)
                {
                    SwitchState(_factory.Jumping());
                }

                break;
        }
    }

    private void OnWindowExit(string action)
    {
        if (action == "Hitbox")
        {
            _ctx._weaponHitbox.SetActive(false);
        }
    }

    // ================= COMBO =================

    private void ExecuteCombo()
    {
        if (_currentIndex < _normalAttackCombo.attacks.Count - 1)
            _currentIndex++;
        else
            _currentIndex = 0;

        _bufferTimer = 0f;
        StartAttackStep();
    }

    // ================= STATE =================

    public override void CheckSwitchState()
    {
        if (_ctx.CharController.isGrounded)
            SwitchState(_factory.Grounded());
        else
            SwitchState(_factory.Falling());
    }

    protected override void ExitState()
    {
        _activeWindows.Clear();

        _ctx.SetAttackLock(false);
        _ctx.SetRotationLock(false);
    }
 
}