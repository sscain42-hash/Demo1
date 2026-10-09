// 1. Định danh actor — mọi thứ trong ComboEngine đều cần cái này
using UnityEngine;

public interface ICombatActor
{
    GameObject GameObject { get; }
    Transform Transform { get; }
    Transform Model { get; }              // có thể null nếu không xoay model
    CharacterEffect Effect { get; }        // có thể null
    bool IsPlayer { get; }
}

// 2. Gây sát thương — Player và Enemy đều implement
public interface IDamageDealer
{
    void ExecuteDamage(GameObject victim, AttackType type);
}

// 3. Parry — chỉ Player cần, nhưng để interface cho sạch
public interface IParryCapable
{
    bool CanParry { get; }
    void RegisterParryWindow(AttackData data, ActionWindow window);
    void UnregisterParry();
}