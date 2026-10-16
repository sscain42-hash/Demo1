
/// <summary>
/// Phân loại state theo hành vi. Dùng để quyết định transition rules
/// thay vì check `is Type` thủ công ở nhiều nơi.
/// </summary>
public enum StateCategory
{
    /// <summary>
    /// Di chuyển tự do: Grounded, Idle, Run, Jump, Fall, Dash.
    /// - Nhận input di chuyển liên tục
    /// - Nhận input attack/parry/dash/jump
    /// - Có gravity
    /// </summary>
    Locomotion,

    /// <summary>
    /// Combat: Attack, Parry, ParryMovement.
    /// - Lock movement (hoặc theo combo step)
    /// - Sở hữu ComboEngine/ParryController
    /// - Có combo window, cancel window
    /// </summary>
    Combat,

    /// <summary>
    /// Override: Hit, Dead.
    /// - Force switch — bất chấp state hiện tại
    /// - Không nhận input (trừ interrupt đặc biệt)
    /// </summary>
    Override,
}