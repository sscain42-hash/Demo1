using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;



public class PlayerInputs : MonoBehaviour
{
    [Serializable]
    public class BufferedInput
    {
        public InputActionType action;

        public float time;

        public Vector2 moveInput;
    }

    // ================= INPUT SYSTEM =================

    public Inputs PlayerInput { get; private set; }

    // ================= RAW INPUT =================

    public Vector2 Move;

    public bool Dash;

    public bool Jump;

    public bool LeftMouse;

    public bool E;

    public bool Q;

    // ================= BUFFER =================

    [Header("Input Buffer")]
    [SerializeField]
    private float _bufferLifetime = 0.5f;

    private readonly Queue<BufferedInput>
        _inputBuffer = new();

    // ================= UNITY =================

    private void Awake()
    {
        PlayerInput = new Inputs();
    }

    private void Update()
    {
        CleanupExpiredInputs();
    }

    private void OnEnable()
    {
        PlayerInput.Player.Enable();

        // MOVE
        PlayerInput.Player.Move.performed +=
            OnMovePressed;

        PlayerInput.Player.Move.canceled +=
            OnMovePressed;

        // JUMP
        PlayerInput.Player.Jump.started +=
            OnJumpPressed;

        PlayerInput.Player.Jump.canceled +=
            OnJumpPressed;

        // DASH
        PlayerInput.Player.Dash.started +=
            OnDashPressed;

        PlayerInput.Player.Dash.canceled +=
            OnDashPressed;

        // ATTACK
        PlayerInput.Player.NormalAttack.started +=
            OnAttackPressed;

        PlayerInput.Player.NormalAttack.canceled +=
            OnAttackPressed;

        // SKILL
        PlayerInput.Player.ElementalSkill.started +=
            OnSkillPressed;

        PlayerInput.Player.ElementalSkill.canceled +=
            OnSkillPressed;

        // BURST
        PlayerInput.Player.ElementalBurst.started +=
            OnSkillSpecialPressed;

        PlayerInput.Player.ElementalBurst.canceled +=
            OnSkillSpecialPressed;
    }

    private void OnDisable()
    {
        // MOVE
        PlayerInput.Player.Move.performed -=
            OnMovePressed;

        PlayerInput.Player.Move.canceled -=
            OnMovePressed;

        // JUMP
        PlayerInput.Player.Jump.started -=
            OnJumpPressed;

        PlayerInput.Player.Jump.canceled -=
            OnJumpPressed;

        // DASH
        PlayerInput.Player.Dash.started -=
            OnDashPressed;

        PlayerInput.Player.Dash.canceled -=
            OnDashPressed;

        // ATTACK
        PlayerInput.Player.NormalAttack.started -=
            OnAttackPressed;

        PlayerInput.Player.NormalAttack.canceled -=
            OnAttackPressed;

        // SKILL
        PlayerInput.Player.ElementalSkill.started -=
            OnSkillPressed;

        PlayerInput.Player.ElementalSkill.canceled -=
            OnSkillPressed;

        // BURST
        PlayerInput.Player.ElementalBurst.started -=
            OnSkillSpecialPressed;

        PlayerInput.Player.ElementalBurst.canceled -=
            OnSkillSpecialPressed;

        PlayerInput.Player.Disable();
    }

    // ================= MOVE =================

    private void OnMovePressed(
        InputAction.CallbackContext context
    )
    {
        Move =
            context.ReadValue<Vector2>();

        if (Move.sqrMagnitude > 0.01f)
        {
            BufferInput(
                InputActionType.Move,
                Move
            );
        }
    }

    // ================= DASH =================

    private void OnDashPressed(
        InputAction.CallbackContext context
    )
    {
        Dash =
            context.ReadValueAsButton();

        // 👉 chỉ buffer lúc nhấn
        if (!context.started)
            return;

        BufferInput(
            InputActionType.Dash
        );
    }

    // ================= JUMP =================

    private void OnJumpPressed(
        InputAction.CallbackContext context
    )
    {
        Jump =
            context.ReadValueAsButton();

        if (!context.started)
            return;

        BufferInput(
            InputActionType.Jump
        );
    }

    // ================= ATTACK =================

    private void OnAttackPressed(
        InputAction.CallbackContext context
    )
    {
        LeftMouse =
            context.ReadValueAsButton();

        if (!context.started)
            return;

        BufferInput(
            InputActionType.NormalAttack
        );
    }

    // ================= SKILL =================

    private void OnSkillPressed(
        InputAction.CallbackContext context
    )
    {
        E =
            context.ReadValueAsButton();

        if (!context.started)
            return;

        BufferInput(
            InputActionType.ElementalSkill
        );
    }

    // ================= BURST =================

    private void OnSkillSpecialPressed(
        InputAction.CallbackContext context
    )
    {
        Q =
            context.ReadValueAsButton();

        if (!context.started)
            return;

        BufferInput(
            InputActionType.ElementalBurst
        );
    }

    // ================= BUFFER =================

    private void BufferInput(
        InputActionType action,
        Vector2 moveInput = default
    )
    {
        // 👉 chống spam input cùng loại
        if (_inputBuffer.Count > 0)
        {
            BufferedInput latest =
                GetLatestInput();

            if (latest != null)
            {
                bool sameAction =
                    latest.action == action;

                bool tooClose =
                    Time.time - latest.time
                    < 0.05f;

                if (sameAction &&
                    tooClose)
                {
                    return;
                }
            }
        }

        _inputBuffer.Enqueue(
            new BufferedInput
            {
                action = action,
                time = Time.time,
                moveInput = moveInput
            }
        );
    }

    // ================= CONSUME =================

    public bool TryConsume(
        InputActionType action
    )
    {
        if (_inputBuffer.Count == 0)
            return false;

        BufferedInput found = null;

        foreach (var input in _inputBuffer)
        {
            if (input.action == action)
            {
                found = input;
                break;
            }
        }

        if (found == null)
            return false;

        RemoveInput(found);
        Debug.Log($"Consumed input: {action} at time {found.time}");
        return true;
    }

    public bool TryConsumeHighestPriority(
        out InputActionType action
    )
    {
        action = InputActionType.None;

        if (_inputBuffer.Count == 0)
            return false;

        BufferedInput best = null;

        int bestPriority = -1;

        foreach (var input in _inputBuffer)
        {
            int priority =
                GetPriority(input.action);

            if (priority > bestPriority)
            {
                bestPriority = priority;
                best = input;
            }
        }

        if (best == null)
            return false;

        action = best.action;

        RemoveInput(best);

        return true;
    }

    // ================= PRIORITY =================

    private int GetPriority(
        InputActionType action
    )
    {
        return action switch
        {
            InputActionType.Dash => 100,

            InputActionType.Jump => 90,

            InputActionType.ElementalBurst => 80,

            InputActionType.ElementalSkill => 70,

            InputActionType.NormalAttack => 60,

            InputActionType.Move => 10,

            _ => 0
        };
    }

    // ================= CLEANUP =================

    private void CleanupExpiredInputs()
    {
        if (_inputBuffer.Count == 0)
            return;

        while (_inputBuffer.Count > 0)
        {
            BufferedInput input =
                _inputBuffer.Peek();

            bool expired =
                Time.time - input.time
                > _bufferLifetime;

            if (expired)
            {
                _inputBuffer.Dequeue();
            }
            else
            {
                break;
            }
        }
    }

    // ================= HELPERS =================

    private BufferedInput GetLatestInput()
    {
        BufferedInput latest = null;

        foreach (var input in _inputBuffer)
        {
            latest = input;
        }

        return latest;
    }

    private void RemoveInput(
        BufferedInput target
    )
    {
        Queue<BufferedInput> temp =
            new();

        while (_inputBuffer.Count > 0)
        {
            BufferedInput current =
                _inputBuffer.Dequeue();

            if (current != target)
            {
                temp.Enqueue(current);
            }
        }

        while (temp.Count > 0)
        {
            _inputBuffer.Enqueue(
                temp.Dequeue()
            );
        }
    }

    // ================= DEBUG =================

    public int BufferedCount =>
        _inputBuffer.Count;
}