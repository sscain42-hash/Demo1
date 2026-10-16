namespace Combat.Parry
{
    /// <summary>
    /// 🔥 Hướng di chuyển sau parry.
    /// 8 hướng + None.
    /// </summary>
    public enum ParryMovementType
    {
        None,           // Không di chuyển (fallback sẽ về Backward)
        Forward,        // Tới
        Backward,       // Lùi (backflip)
        Left,           // Sang trái
        Right,          // Sang phải
        ForwardLeft,    // Chéo trước trái
        ForwardRight,   // Chéo trước phải
        BackwardLeft,   // Chéo sau trái
        BackwardRight,  // Chéo sau phải
    }
}