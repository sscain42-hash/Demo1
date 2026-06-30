using UnityEngine;

public class PlayerIdleState : PlayerBaseState
{
    private const float INPUT_THRESHOLD = 0.01f;

    public PlayerIdleState(PlayerController ctx, PlayerStateFactory factory)
        : base(ctx, factory) { }

    public override void EnterState()
    {
        if (_ctx.Animator != null)
        {
            // 🎯 GIẢI PHÁP MƯỢT MÀ:
            // Sử dụng CrossFadeInFixedTime để ép Animator phải hòa trộn bất chấp trạng thái nghẽn.
            // - Tham số thứ 2 (0.15f): Thời gian hòa trộn tính bằng giây. Giúp tư thế chém hạ xuống tư thế đứng yên cực mượt.
            // - Tham số thứ 3 (0): Ép chạy trên Layer 0 (Base Layer).
            // - Tham số thứ 4 (0f): Ép hoạt ảnh Idle phải phát ĐÈ từ giây đầu tiên (0f) của clip Idle, không cho phép lấy bộ đệm cũ.
            _ctx.Animator.CrossFadeInFixedTime(_ctx.ID_Idle, 0.15f, 0, 0f);
        }

        Debug.Log("<color=green>➔ ĐÃ CHUYỂN MƯỢT MÀ VỀ IDLE STATE!</color>");
    }
    protected override void UpdateState()
    {
        // Stop horizontal movement
        _ctx.SetVelocity(_ctx.Velocity.x * 0f, _ctx.Velocity.y, _ctx.Velocity.z * 0f);
        CheckSwitchState();
      
    }

    protected override void ExitState() { }

    public override void CheckSwitchState()
    {
        if (_ctx.InputVector.magnitude > INPUT_THRESHOLD)
            SwitchState(_factory.Run());
      
    }

    public override void InitializeSubState() { }       
}