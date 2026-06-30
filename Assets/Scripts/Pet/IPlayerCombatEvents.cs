using System;

public interface IPlayerCombatEvents
{
    // Chỉ cần 1 event này cho TẤT CẢ các loại kỹ năng
    event Action<AttackType> OnPlayerSkillCast;
}