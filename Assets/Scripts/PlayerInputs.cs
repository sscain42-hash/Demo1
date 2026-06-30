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
        _commandBuffer.Enqueue(new InputCommand(action, Time.unscaledTime));
    }

    private bool HasRecentInput(BufferedAction action)
    {
        foreach (var cmd in _commandBuffer)
        {
            if (cmd.action != action) continue;

            // 🟢 SỬA THÀNH: unscaledTime
            if (Time.unscaledTime - cmd.timestamp < 0.02f)
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
            if (Time.unscaledTime - cmd.timestamp > bufferTime)
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
}