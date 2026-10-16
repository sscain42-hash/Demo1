using UnityEngine;

public interface ICombatActor
{
    GameObject GameObject { get; }
    Transform Transform { get; }
    Transform Model { get; }
    CharacterEffect Effect { get; }
    bool IsPlayer { get; }
}

public interface IDamageDealer
{
    void ExecuteDamage(GameObject victim, AttackType type);
}

public interface IParryCapable
{
    bool CanParry { get; }
    void RegisterParryWindow(AttackData data, ActionWindow window);
    void UnregisterParry();
}