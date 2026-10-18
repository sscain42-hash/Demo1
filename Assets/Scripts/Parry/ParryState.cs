namespace Combat.Parry
{
    /// <summary>
    /// Các trạng thái của hệ thống parry.
    /// 
    /// FLOW:
    /// Idle → (input) → WindowOpen → (hitbox) → Success → (grace) → Idle
    ///                              → (timeout) → Fail → Idle
    ///                              → (no resource) → Blocking
    /// </summary>
    public enum ParryState
    {
        /// <summary>Không có parry nào đang active. Sẵn sàng nhận input.</summary>
        Idle,

        /// <summary>Cửa sổ parry đang mở, chờ hitbox enemy va chạm.</summary>
        WindowOpen,

        /// <summary>Parry vừa thành công, đang trong grace period cho chain parry.</summary>
        Success,

        /// <summary>Cửa sổ hết hạn mà không có hitbox → parry thất bại.</summary>
        Fail,

        /// <summary>Đang block thường (không parry được — hết resource hoặc spam).</summary>
        Blocking,

        /// <summary>Đang trong cooldown sau khi parry (không thể parry tiếp ngay).</summary>
        Cooldown,
        ParryMovement
    }
}
