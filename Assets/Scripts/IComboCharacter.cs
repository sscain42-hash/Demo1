public interface IComboCharacter
{
    // Giúp Event lấy được thông tin đòn đánh hiện tại (để biết tên Animation, các ô Window)
    AttackData CurrentAttackData { get; }

    // Giúp Event lấy được kiểu đòn đánh (Normal, Skill E, Burst Q) để truyền vào Pooler VFX
    AttackType CurrentRuntimeAttackType { get; }
}