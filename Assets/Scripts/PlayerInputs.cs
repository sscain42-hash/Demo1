using System;
using UnityEngine;
using UnityEngine.InputSystem;



public partial class PlayerInputs : MonoBehaviour
{
    [Header("Buffer Settings")]
    [SerializeField] private float bufferDuration = 0.15f; // Thời gian sống tối đa của 1 lệnh (150ms)

    // Lưu duy nhất 1 lệnh đang chờ xử lý (Slot-based đè lệnh)
    private InputCommand? _bufferedCommand;

    public struct InputCommand
    {
        public BufferedAction action;
        public float timestamp;

        public InputCommand(BufferedAction action, float timestamp)
        {
            this.action = action;
            this.timestamp = timestamp;
        }
    }

    // =========================================================
    // INPUT VALUES (Read-only properties cho FSM & Cinemachine)
    // =========================================================
    public Vector2 Move { get; private set; }
    public Vector2 Look { get; private set; } // Trục xoay Camera (Mouse Delta / Right Stick)
    public bool JumpHeld { get; private set; }

    private Inputs _input;

    private void Awake()
    {
        _input = new Inputs();
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
        // Tự động hết hạn Buffer nếu quá thời gian bufferDuration
        if (_bufferedCommand.HasValue && (Time.unscaledTime - _bufferedCommand.Value.timestamp > bufferDuration))
        {
            _bufferedCommand = null;
        }
    }

    // =========================================================
    // INPUT BUFFERING (Smart Overwrite)
    // =========================================================

    private void BufferAction(BufferedAction action)
    {
        // 🔥 ĐÈ LỆNH: Nút bấm mới nhất luôn ghi đè lệnh cũ lập tức
        _bufferedCommand = new InputCommand(action, Time.unscaledTime);
    }

    public bool HasCommand(BufferedAction action)
    {
        if (!_bufferedCommand.HasValue) return false;
        return _bufferedCommand.Value.action == action;
    }

    /// <summary>
    /// Đọc và xóa lệnh trong Buffer (Chỉ gọi hàm này khi State thực sự sẵn sàng nhận Input)
    /// </summary>
    public bool ConsumeCommand(BufferedAction action)
    {
        if (HasCommand(action))
        {
            _bufferedCommand = null; // Nuốt lệnh
            return true;
        }
        return false;
    }

    /// <summary>
    /// Xóa sạch Buffer khi FSM đổi State
    /// </summary>
    public void ClearBuffer()
    {
        _bufferedCommand = null;
    }

    // =========================================================
    // REGISTER INPUTS (New Input System Event Callbacks)
    // =========================================================

    private void RegisterGameplayInputs()
    {
        // 1. Movement Vector (WASD / Left Stick)
        _input.Player.Move.performed += OnMovePerformed;
        _input.Player.Move.canceled += OnMoveCanceled;

        // 2. Camera Look Vector (Mouse Delta / Right Stick)
        _input.Player.Look.performed += OnLookPerformed;
        _input.Player.Look.canceled += OnLookCanceled;

        // 3. Jump (Buffer + Hold)
        _input.Player.Jump.performed += OnJumpPerformed;
        _input.Player.Jump.canceled += OnJumpCanceled;

        // 4. Actions (Buffered)
        _input.Player.Dash.performed += OnDashPerformed;
        _input.Player.NormalAttack.performed += OnAttackPerformed;
        _input.Player.ElementalSkill.performed += OnSkillPerformed;
        _input.Player.ElementalBurst.performed += OnBurstPerformed;
    }

    private void UnregisterGameplayInputs()
    {
        _input.Player.Move.performed -= OnMovePerformed;
        _input.Player.Move.canceled -= OnMoveCanceled;

        _input.Player.Look.performed -= OnLookPerformed;
        _input.Player.Look.canceled -= OnLookCanceled;

        _input.Player.Jump.performed -= OnJumpPerformed;
        _input.Player.Jump.canceled -= OnJumpCanceled;

        _input.Player.Dash.performed -= OnDashPerformed;
        _input.Player.NormalAttack.performed -= OnAttackPerformed;
        _input.Player.ElementalSkill.performed -= OnSkillPerformed;
        _input.Player.ElementalBurst.performed -= OnBurstPerformed;
    }

    // --- Handlers ---
    private void OnMovePerformed(InputAction.CallbackContext ctx) => Move = ctx.ReadValue<Vector2>();
    private void OnMoveCanceled(InputAction.CallbackContext ctx) => Move = Vector2.zero;

    private void OnLookPerformed(InputAction.CallbackContext ctx) => Look = ctx.ReadValue<Vector2>();
    private void OnLookCanceled(InputAction.CallbackContext ctx) => Look = Vector2.zero;

    private void OnJumpPerformed(InputAction.CallbackContext ctx)
    {
        JumpHeld = true;
        BufferAction(BufferedAction.Jump);
    }
    private void OnJumpCanceled(InputAction.CallbackContext ctx) => JumpHeld = false;

    private void OnDashPerformed(InputAction.CallbackContext ctx) => BufferAction(BufferedAction.Dash);
    private void OnAttackPerformed(InputAction.CallbackContext ctx) => BufferAction(BufferedAction.NormalAttack);
    private void OnSkillPerformed(InputAction.CallbackContext ctx) => BufferAction(BufferedAction.ElementalSkill);
    private void OnBurstPerformed(InputAction.CallbackContext ctx) => BufferAction(BufferedAction.ElementalBurst);
}