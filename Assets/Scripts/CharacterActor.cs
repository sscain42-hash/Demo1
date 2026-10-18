using UnityEngine;
using Combat.Parry;
using System;
/// <summary>
/// Base class cho mọi nhân vật chiến đấu (Player, Enemy, NPC).
/// Chứa: Status, Combat references, Parry, Effects, Damage logic.
/// KHÔNG chứa: Input, Movement, FSM.
/// </summary>
[RequireComponent(typeof(ParryController))]
public abstract class CharacterActor : Damageable,
                                       ICombatActor,
                                       IDamageDealer,
                                       IComboCharacter,
                                       IParryCapable,
                                       IParryReceiver
{
    // ═══════════════════════════════════════════════════════════
    // STATUS
    // ═══════════════════════════════════════════════════════════
    [Header("=== Character Config ===")]
    [SerializeField] protected SO_CharacterConfiguration baseConfig;

    public StatusHandle Health { get; protected set; }
    public SO_CharacterConfiguration RuntimeConfig { get; protected set; }

    public event Action<CharacterActor> OnTakeDamageEvent;
    public event Action<CharacterActor> OnDieEvent;

    // ═══════════════════════════════════════════════════════════
    // REFERENCES
    // ═══════════════════════════════════════════════════════════
    [Header("=== Core References ===")]
    [SerializeField] protected Animator animator;
    [SerializeField] protected Transform model;
    [SerializeField] protected Transform swordTransform;
    [SerializeField] protected CharacterEffect characterEffect;

    public Animator Animator => animator;
    public Transform Model => model;
    public Transform SwordTransform => swordTransform;
    public CharacterEffect Effect => characterEffect;

    // ═══════════════════════════════════════════════════════════
    // COMBAT
    // ═══════════════════════════════════════════════════════════
   

    protected ComboEngineBase _comboEngine;
    public AttackData CurrentAttackData => _comboEngine?.CurrentAttackData;
    public AttackType CurrentRuntimeAttackType { get; protected set; }

    // Cho Enemy set từ BT


    // ═══════════════════════════════════════════════════════════
    // PARRY
    // ═══════════════════════════════════════════════════════════
    protected ParryController _parryController;
    public ParryController ParryController => _parryController;
    public bool CanParry => true;

    // ═══════════════════════════════════════════════════════════
    // HIT REACTION CONFIG
    // ═══════════════════════════════════════════════════════════
    [Header("=== Hit Reaction ===")]
    [field: SerializeField] public float HitStunDuration { get; protected set; } = 0.5f;
    [field: SerializeField] public float HitReactionIFrameDuration { get; protected set; } = 0.5f;

    // ═══════════════════════════════════════════════════════════
    // KNOCKBACK CONFIG
    // ═══════════════════════════════════════════════════════════
    [Header("=== Knockback ===")]
    [SerializeField] protected float knockBackForce = 15f;
    [SerializeField] protected AnimationCurve knockBackcurve;

    // ═══════════════════════════════════════════════════════════
    // ABSTRACT API
    // ═══════════════════════════════════════════════════════════

    public abstract bool IsPlayer { get; }
    public abstract Vector3 GetLookDirection();
    public abstract Transform GetAttackTarget();
    protected abstract ComboEngineBase CreateComboEngine();
    protected abstract float GetDamageMultiplier(AttackType type);

    /// <summary>Hook khi actor bị target parry. Override nếu cần.</summary>
    protected virtual void OnAttackParried(GameObject target) { }

    /// <summary>Check i-frame trước khi nhận damage.</summary>
    protected virtual bool CanTakeDamage() => true;

    // ═══════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════════════
    protected virtual void Awake()
    {
        Health = new StatusHandle();

        if (baseConfig != null)
            RuntimeConfig = Instantiate(baseConfig);

        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (model == null && animator != null) model = animator.transform;
        if (characterEffect == null) characterEffect = GetComponent<CharacterEffect>();
        _parryController = GetComponent<ParryController>();

        _comboEngine = CreateComboEngine();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        Health.OnDieEvent += HandleDeath;
        InitializeStats();
    }

    protected override void OnDisable()
    {
        Health.OnDieEvent -= HandleDeath;
        base.OnDisable();
    }

    protected virtual void InitializeStats()
    {
        if (RuntimeConfig == null) return;

        int hp = RuntimeConfig.GetHP();
        Health.InitValue(hp, hp);
    }

    // ═══════════════════════════════════════════════════════════
    // DAMAGE — OUTGOING
    // ═══════════════════════════════════════════════════════════
    public override void CauseDMG(GameObject target, AttackType type)
    {
        if (target == null) return;

        // Parry check
        if (target.TryGetComponent<IParryReceiver>(out var parryReceiver))
        {
            if (parryReceiver.TryParry(gameObject))
            {
                OnAttackParried(target);
                return;
            }
        }

        // Damage
        if (!DamageableData.Contains(target, out var receiver)) return;

        int damage = CalculateDamage(type);
        bool isCrit = RuntimeConfig != null && RuntimeConfig.IsCRIT;

        receiver.TakeDMG(damage, isCrit);

        // Knockback
        if (target.TryGetComponent<IKnockbackable>(out var kb))
        {
            Vector3 dir = GetLookDirection();
            dir.y = 0;
            kb.OnKnockback(dir, knockBackForce, knockBackcurve);
        }
    }

    public void ExecuteDamage(GameObject victim, AttackType type) => CauseDMG(victim, type);

    protected virtual int CalculateDamage(AttackType type)
    {
        if (RuntimeConfig == null) return 0;

        float mult = GetDamageMultiplier(type);
        int baseATK = RuntimeConfig.GetATK();
        int scaled = Mathf.CeilToInt(baseATK * mult);

        if (RuntimeConfig.IsCRIT)
            scaled = Mathf.CeilToInt(scaled * (1 + RuntimeConfig.GetCRITDMG() / 100f));

        return scaled;
    }

    // ═══════════════════════════════════════════════════════════
    // DAMAGE — INCOMING
    // ═══════════════════════════════════════════════════════════
    public override void TakeDMG(int damage, bool isCRIT)
    {
        if (!CanTakeDamage()) return;

        Health.Decreases(damage);
        OnTakeDamageEvent?.Invoke(this);
    }

    protected virtual void HandleDeath()
    {
        OnDieEvent?.Invoke(this);
        gameObject.SetActive(false);
    }

    // ═══════════════════════════════════════════════════════════
    // PARRY
    // ═══════════════════════════════════════════════════════════
    public bool TryParry(GameObject attacker)
    {
        if (_parryController == null) return false;
        return _parryController.TryParry(attacker);
    }

    public void RegisterParryWindow(AttackData data, ActionWindow window) { }
    public void UnregisterParry() { }

    // ═══════════════════════════════════════════════════════════
    // ICombatActor / IComboCharacter
    // ═══════════════════════════════════════════════════════════
    public GameObject GameObject => gameObject;
    public Transform Transform => transform;
}
