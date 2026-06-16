using UnityEngine;

public interface IDamageProvider
{
    // Hàm nhận lệnh gây sát thương tổng quát từ bất kỳ nguồn nào (Melee, Projectile)
    void ExecuteDamage(GameObject victim, AttackType attackType);
}