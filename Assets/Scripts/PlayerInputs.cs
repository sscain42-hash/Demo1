using System;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class PlayerInputs : MonoBehaviour
{
    [Header("Buffer Settings")]
    [SerializeField] private float bufferDuration = 0.15f; // Thời gian sống tối đa của 1 lệnh (150ms là chuẩn)

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

    public Vector2 Move { get; private set; }
    public bool JumpHeld { get; private set; }

    private Inputs _input;

    private void Awake() => _input = new Inputs();

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
    /// Xóa sạch Buffer khi FSM đổi State (Grounded -> Falling, Attack -> Hurt...)
    /// </summary>
    public void ClearBuffer()
    {
        _bufferedCommand = null;
    }

    // =========================================================
    // REGISTER INPUTS
    // =========================================================

    private void RegisterGameplayInputs()
    {
        _input.Player.Move.performed += ctx => Move = ctx.ReadValue<Vector2>();
        _input.Player.Move.canceled += _ => Move = Vector2.zero;

        _input.Player.Jump.performed += _ => { JumpHeld = true; BufferAction(BufferedAction.Jump); };
        _input.Player.Jump.canceled += _ => JumpHeld = false;

        _input.Player.Dash.performed += _ => BufferAction(BufferedAction.Dash);
        _input.Player.NormalAttack.performed += _ => BufferAction(BufferedAction.NormalAttack);
        _input.Player.ElementalSkill.performed += _ => BufferAction(BufferedAction.ElementalSkill);
        _input.Player.ElementalBurst.performed += _ => BufferAction(BufferedAction.ElementalBurst);
    }

    private void UnregisterGameplayInputs()
    {
        // Unregister logic ở đây
    }
}