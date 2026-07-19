using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class PlayerInputs : MonoBehaviour
{
    // =========================================================
    // REALTIME INPUT
    // =========================================================

    public Vector2 Move { get; private set; }

    public bool JumpHeld { get; private set; }

    [Serializable]
    public struct InputCommand
    {
        public BufferedAction action;
        public float timestamp;

        public InputCommand(
            BufferedAction action,
            float timestamp)
        {
            this.action = action;
            this.timestamp = timestamp;
        }
    }

    [Header("Input Buffer")]
    [SerializeField]
    private float bufferTime = 0.2f;

    [SerializeField]
    private int maxBufferSize = 10;

    private readonly Queue<InputCommand>
        _commandBuffer = new();

    // =========================================================
    // INPUT SYSTEM
    // =========================================================

    private Inputs _input;

    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        _input =
            new Inputs();
    }

    private void OnEnable()
    {
        _input.Enable();

        RegisterGameplayInputs();
    }

    private void OnDisable()
    {
        UnregisterGameplayInputs();

        _input.Disable();
    }

    private void Update()
    {
        CleanupExpiredCommands();
    }

    // =========================================================
    // REGISTER
    // =========================================================

    private void RegisterGameplayInputs()
    {
        // ================= MOVE =================

        _input.Player.Move.performed +=
            OnMovePerformed;

        _input.Player.Move.canceled +=
            OnMoveCanceled;

        // ================= JUMP =================

        _input.Player.Jump.performed +=
            OnJumpPerformed;

        _input.Player.Jump.canceled +=
            OnJumpCanceled;

        // ================= COMMANDS =================

        _input.Player.Dash.performed +=
            _ => AddCommand(
                BufferedAction.Dash);

        _input.Player.NormalAttack.performed +=
            _ => AddCommand(
                BufferedAction.NormalAttack);

        _input.Player.ElementalSkill.performed +=
            _ => AddCommand(
                BufferedAction.ElementalSkill);

        _input.Player.ElementalBurst.performed +=
            _ => AddCommand(
                BufferedAction.ElementalBurst);
    }

    private void UnregisterGameplayInputs()
    {
        _input.Player.Move.performed -=
            OnMovePerformed;

        _input.Player.Move.canceled -=
            OnMoveCanceled;

        _input.Player.Jump.performed -=
            OnJumpPerformed;

        _input.Player.Jump.canceled -=
            OnJumpCanceled;
    }

    // =========================================================
    // MOVE
    // =========================================================

    private void OnMovePerformed(
        InputAction.CallbackContext ctx)
    {
        Move =
            ctx.ReadValue<Vector2>();
    }

    private void OnMoveCanceled(
        InputAction.CallbackContext ctx)
    {
        Move = Vector2.zero;
    }

    // =========================================================
    // JUMP
    // =========================================================

    private void OnJumpPerformed(
        InputAction.CallbackContext ctx)
    {
        JumpHeld = true;

        AddCommand(
            BufferedAction.Jump);
    }

    private void OnJumpCanceled(
        InputAction.CallbackContext ctx)
    {
        JumpHeld = false;
    }

    // =========================================================
    // BUFFER
    // =========================================================

    private void AddCommand(BufferedAction action)
    {
        if (HasRecentInput(action))
            return;

        if (_commandBuffer.Count >= maxBufferSize)
        {
            _commandBuffer.Dequeue();
        }

        // 🟢 SỬA THÀNH: unscaledTime
        _commandBuffer.Enqueue(new InputCommand(action, Time.time));
    }

    private bool HasRecentInput(BufferedAction action)
    {
        foreach (var cmd in _commandBuffer)
        {
            if (cmd.action != action) continue;

            // 🟢 SỬA THÀNH: unscaledTime
            if (Time.time - cmd.timestamp < 0.02f)
            {
                return true;
            }
        }
        return false;
    }

    private void CleanupExpiredCommands()
    {
        while (_commandBuffer.Count > 0)
        {
            InputCommand cmd = _commandBuffer.Peek();

            // 🟢 SỬA THÀNH: unscaledTime
            if (Time.time - cmd.timestamp > bufferTime)
            {
                _commandBuffer.Dequeue();
            }
            else
            {
                break;
            }
        }
    }

    // =========================================================
    // QUERY
    // =========================================================

    public bool HasCommand(
        BufferedAction action)
    {
        CleanupExpiredCommands();

        foreach (var cmd in _commandBuffer)
        {
            if (cmd.action == action)
                return true;
        }

        return false;
    }

    // =========================================================
    // CONSUME
    // =========================================================

    public bool ConsumeCommand(
        BufferedAction action)
    {
        CleanupExpiredCommands();

        if (_commandBuffer.Count == 0)
            return false;

        bool found = false;

        Queue<InputCommand> temp =
            new();

        while (_commandBuffer.Count > 0)
        {
            InputCommand cmd =
                _commandBuffer.Dequeue();

            if (!found &&
                cmd.action == action)
            {
                found = true;
                continue;
            }

            temp.Enqueue(cmd);
        }

        while (temp.Count > 0)
        {
            _commandBuffer.Enqueue(
                temp.Dequeue());
        }

        return found;
    }

    // =========================================================
    // DEBUG
    // =========================================================

    public void ClearBuffer()
    {
        _commandBuffer.Clear();
    }

    public int BufferCount =>
        _commandBuffer.Count;
    // =========================================================
    // PRIORITY BASED QUERY
    // =========================================================

    /// <summary>
    /// Trả về điểm ưu tiên của từng hành động (Số càng cao càng ưu tiên)
    /// </summary>
    private int GetActionPriority(BufferedAction action)
    {
        return action switch
        {
            BufferedAction.ElementalBurst => 3,   // Q: Ưu tiên tối cao
            BufferedAction.ElementalSkill => 2,   // E: Ưu tiên trung bình
            BufferedAction.NormalAttack => 1,   // Normal: Ưu tiên thấp nhất
            _ => 0
        };
    }

    /// <summary>
    /// Tìm hành động có ưu tiên cao nhất trong Buffer. 
    /// Nếu độ ưu tiên bằng nhau, hành động nào bấm trước (FIFO) sẽ được chọn.
    /// </summary>
    public BufferedAction? GetHighestPriorityBufferedAction(params BufferedAction[] actionsToCheck)
    {
        CleanupExpiredCommands();

        BufferedAction? bestAction = null;
        int highestPriority = -1;
        float earliestTimestamp = float.MaxValue;

        foreach (var cmd in _commandBuffer)
        {
            foreach (var action in actionsToCheck)
            {
                if (cmd.action != action) continue;

                int currentPriority = GetActionPriority(cmd.action);

                // Trường hợp 1: Tìm thấy đòn có ưu tiên cao hơn hẳn (Ví dụ: Q đè E)
                if (currentPriority > highestPriority)
                {
                    highestPriority = currentPriority;
                    bestAction = cmd.action;
                    earliestTimestamp = cmd.timestamp;
                }
                // Trường hợp 2: Cùng độ ưu tiên (Ví dụ: Normal và Normal), áp dụng FIFO (chọn đòn bấm trước)
                else if (currentPriority == highestPriority && cmd.timestamp < earliestTimestamp)
                {
                    earliestTimestamp = cmd.timestamp;
                    bestAction = cmd.action;
                }
            }
        }

        return bestAction;
    }
}