using System.Collections.Generic;
using UnityEngine;

public static class ParryRegistry
{
    private class ParryInfo
    {
        public GameObject defender;
        public AttackData parryData;
        public ActionWindow window;

        // 🔥 CHỈ CẦN 1 FLAG — đã parry trong animation này chưa?
        public bool hasParried;
    }

    private static readonly Dictionary<GameObject, ParryInfo> _activeParries
        = new Dictionary<GameObject, ParryInfo>();

    public static void Register(GameObject defender, AttackData parryData, ActionWindow window)
    {
        if (defender == null || parryData == null || window == null) return;

        // 🔥 Nếu đã có entry → KHÔNG reset hasParried
        // (vì Register được gọi mỗi frame trong window)
        if (_activeParries.TryGetValue(defender, out var existing))
        {
            existing.parryData = parryData;
            existing.window = window;
            return;
        }

        // Tạo entry mới → hasParried = false
        _activeParries[defender] = new ParryInfo
        {
            defender = defender,
            parryData = parryData,
            window = window,
            hasParried = false
        };
    }

    public static void Unregister(GameObject defender)
    {
        if (defender == null) return;
        _activeParries.Remove(defender);
    }

    public static bool IsInParryWindow(GameObject defender)
    {
        return defender != null && _activeParries.ContainsKey(defender);
    }

    public static bool TryParry(GameObject defender, GameObject attacker)
    {
        if (defender == null || attacker == null) return false;

        if (!_activeParries.TryGetValue(defender, out var info)) return false;

        // 🔥 ĐÃ PARRY RỒI → không parry nữa
        // Vẫn return true để nuốt damage (không gây sát thương player)
        // Nhưng KHÔNG trigger reward
        if (info.hasParried)
        {
            Debug.Log($"[ParryRegistry] Đã parry trong animation này rồi — nuốt damage, không reward");
            return true;
        }

        // 🔥 ĐÁNH DẤU ĐÃ PARRY
        info.hasParried = true;

        // Trigger reward (chỉ 1 lần)
        TriggerParryReward(info, attacker);

        return true;
    }
  /*  private static void TriggerParryReward(ParryInfo info, GameObject attacker)
    {
        GameObject defender = info.defender;
        if (defender == null) return;

        var feedback = defender.GetComponent<IParryFeedback>();
        feedback?.OnParrySuccess(attacker);
    }*/
    private static void TriggerParryReward(ParryInfo info, GameObject attacker)
    {
        GameObject defender = info.defender;
        if (defender == null) return;

        // 1. HitStop
        if (HitStopSystem.Instance != null)
            HitStopSystem.Instance.Trigger(0.2f, 0.02f);

        // 2. Screen Shake
        if (ScreenShakeManager.Instance != null)
            ScreenShakeManager.Instance.TriggerShake(0.6f);

        // 3. Flash
        if (ParryFlashEffect.Instance != null)
            ParryFlashEffect.Instance.Flash();

        // 4. Xoay nhân vật snap
        RotateDefenderToAttacker(defender, attacker);

        // 5. VFX
        SpawnParryVFX(defender, attacker);

        // 6. SFX
        PlayParrySFX(info);

        Debug.Log($"[Parry] {defender.name} PARRY thành công đòn từ {attacker.name}!");
    }

    private static void RotateDefenderToAttacker(GameObject defender, GameObject attacker)
    {
        Vector3 dirToAttacker = attacker.transform.position - defender.transform.position;
        dirToAttacker.y = 0;

        if (dirToAttacker.sqrMagnitude < 0.001f) return;

        var playerCtx = defender.GetComponent<PlayerController>();
        if (playerCtx != null && playerCtx.Model != null)
        {
            playerCtx.Model.rotation = Quaternion.LookRotation(dirToAttacker.normalized);
            return;
        }

        defender.transform.rotation = Quaternion.LookRotation(dirToAttacker.normalized);
    }

    private static void SpawnParryVFX(GameObject defender, GameObject attacker)
    {
        Vector3 midPoint = (defender.transform.position + attacker.transform.position) * 0.5f;
        midPoint.y = defender.transform.position.y + 1.2f;

        Vector3 dirToAttacker = (attacker.transform.position - defender.transform.position);
        dirToAttacker.y = 0;
        Quaternion vfxRotation = dirToAttacker.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(dirToAttacker.normalized)
            : defender.transform.rotation;

        if (ParryFlashEffect.Instance != null)
        {
            ParryFlashEffect.Instance.SpawnShockwave(midPoint, vfxRotation);
            ParryFlashEffect.Instance.SpawnSpark(midPoint, vfxRotation);
        }
    }

    private static void PlayParrySFX(ParryInfo info)
    {
        if (SoundManager.Instance == null) return;

        if (ParryFlashEffect.Instance != null && ParryFlashEffect.Instance.ClangSFX != null)
        {
            SoundManager.Instance.PlaySFX(
                ParryFlashEffect.Instance.ClangSFX,
                minPitch: 0.95f,
                maxPitch: 1.05f
            );
        }
    }

    public static void ClearAll()
    {
        _activeParries.Clear();
    }
}