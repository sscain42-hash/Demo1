using System;
using System.Collections;
using System.Collections.Generic;
using NaughtyAttributes;
using NodeCanvas.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Events;
using Random = UnityEngine.Random;

public class EnemyController : Damageable, IPooled<EnemyController>,
                                ICombatActor, IDamageDealer
{
    #region === Serialized ===

    [field: SerializeField, Required]
    public Blackboard Blackboard { get; private set; }

    [SerializeField] private SO_EnemyConfiguration baseConfig;

    [Header("Layer")]
    [SerializeField] private LayerMask mainLayer;
    [SerializeField] private LayerMask ignoreLayer;

    #endregion

    #region === Public State ===

    public SO_EnemyConfiguration RuntimeConfig { get; private set; }
    public StatusHandle Health { get; private set; }

    public UnityEvent<EnemyController> OnTakeDamageEvent;
    public UnityEvent<EnemyController> OnDieEvent;

    #endregion

    #region === Private Fields ===

    private PlayerController _player;
    private int _attackCount;
    private EnemyAttack _enemyAttackRef;

    private CharacterEffect _combatEffect;

    [Header("Combat")]
    [SerializeField] private Transform swordTransform;

    #endregion

    public Animator Animator { get => _animator; private set => _animator = value; }

    #region === Unity Lifecycle ===

    private CharacterController _characterController;

    private void Awake()
    {
        Health = new StatusHandle();
        RuntimeConfig = Instantiate(baseConfig);
        _originalLocalPos = transform.localPosition;
        _animator = GetComponent<Animator>();
        _enemyAttackRef = GetComponent<EnemyAttack>();
        _combatEffect = GetComponent<CharacterEffect>();
        _characterController = GetComponent<CharacterController>();
        if (_characterController != null)
            _characterController.enabled = false;
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        ResolveDependencies();
        RegisterEvents();
        SyncBlackboardBase();

        InitializeStats();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        UnregisterEvents();
    }

    private void Update()
    {
    }
    #endregion

    #region === Dependency ===

    private void ResolveDependencies()
    {
        gameObject.SetObjectLayer(mainLayer.value);

        _player = FindAnyObjectByType<PlayerController>();
        if (_player == null)
        {
            Debug.LogError($"[EnemyController] PlayerController not found for enemy '{name}'. Disabling behaviour until player is available.");
            return;
        }

        _enemyAttackRef = GetComponent<EnemyAttack>();
    }

    private void RegisterEvents()
    {
        Health.OnDieEvent += HandleDeath;
    }

    private void UnregisterEvents()
    {
        Health.OnDieEvent -= HandleDeath;
    }

    #endregion

    #region === Initialization ===

    private void InitializeStats()
    {
        int maxHP = RuntimeConfig.GetHP();
        Health.InitValue(maxHP, maxHP);

        SetWalkSpeed(RuntimeConfig.GetWalkSpeed());
        SetRunSpeed(RuntimeConfig.GetRunSpeed());
    }

    private void SyncBlackboardBase()
    {
        Blackboard.SetVariableValue("RootPosition", transform.position);
        SetDie(false);
        SetTakeDMG(false);
    }

    #endregion

    #region === Combat System ===

    public override void CauseDMG(GameObject target, AttackType type)
    {
        if (!DamageableData.Contains(target, out var receiver)) return;

        // 🔥 PARRY CHECK ĐÃ ĐƯỢC XỬ LÝ TRONG ComboEngine.ProcessBoxCastHitbox()
        // Không check lại ở đây — tránh double check và tránh parry bị trigger 2 lần

        // Knockback (chỉ khi không bị parry)
        var knockbackTarget = target.GetComponent<IKnockbackable>();
        if (knockbackTarget != null)
        {
            Vector3 dir = GetLookDirection();
            dir.y = 0;
            knockbackTarget.OnKnockback(dir, knockBackForce, knockBackcurve);
        }

        int damage = CalculateDamage(RuntimeConfig.GetATK(), out bool isCrit);
        receiver.TakeDMG(damage, isCrit);
    }

    public Vector3 GetLookDirection()
    {
       

  
            return Model != null ? Model.forward : transform.forward;
       
    }
    private int CalculateDamage(float percent, out bool isCrit)
    {
        if (RuntimeConfig == null)
        {
            Debug.LogError($"[EnemyController] RuntimeConfig is null on '{name}'. Using default values.");
            isCrit = false;
            return 0;
        }

        int baseATK = RuntimeConfig.GetATK();
        int scaled = Mathf.CeilToInt(baseATK * (percent / 100f));

        isCrit = RuntimeConfig.IsCRIT;

        if (isCrit)
        {
            float critMulti = 1 + RuntimeConfig.GetCRITDMG() / 100f;
            scaled = Mathf.CeilToInt(scaled * critMulti);
        }

        return scaled;
    }

    public override void TakeDMG(int damage, bool isCRIT)
    {
        int finalDamage = Mathf.Max(0, damage);
        Health.Decreases(finalDamage);

        SetTakeDMG(true);
        OnTakeDamageEvent?.Invoke(this);
    }

    #endregion

    #region === Death ===

    private void HandleDeath()
    {
        gameObject.SetObjectLayer(ignoreLayer.value);
        SetDie(true);
        SetTakeDMG(false);

        OnDieEvent?.Invoke(this);
        gameObject.SetActive(false);
    }

    #endregion

    #region === Multipliers ===

    public void SetAttackCount(int value) => _attackCount = value;

    #endregion

    #region === Blackboard Setters ===

    public void SetTakeDMG(bool v) => Blackboard.SetVariableValue("TakeDMG", v);
    public void SetDie(bool v) => Blackboard.SetVariableValue("Die", v);
    private void SetWalkSpeed(float v) => Blackboard.SetVariableValue("WalkSpeed", v);
    private void SetRunSpeed(float v) => Blackboard.SetVariableValue("RunSpeed", v);

    #endregion

    #region === Pool ===

    public void Release() => ReleaseCallback?.Invoke(this);

    private Coroutine _flinchRoutine;
    private Vector3 _originalLocalPos;
    private Animator _animator;
    private Transform _currentTarget;
    internal CharacterController CharacterController;
    public float knockBackForce;
    public AnimationCurve knockBackcurve;

    #endregion

    public Action<EnemyController> ReleaseCallback { get; set; }

    #region ICombatActor / IDamageDealer

    public GameObject GameObject => gameObject;
    public Transform Transform => transform;

    public Transform Model
    {
        get
        {
            if (_animator != null) return _animator.transform;
            return transform;
        }
    }

    public CharacterEffect Effect => _combatEffect;
    public bool IsPlayer => false;

    public Transform SwordTransform => swordTransform;

    public void ExecuteDamage(GameObject victim, AttackType type)
    {
        if (_enemyAttackRef != null)
            _enemyAttackRef.ExecuteDamage(victim, type);
  
    }

    public Transform CurrentTarget { get; private set; }
    public void SetCurrentTarget(Transform target) => CurrentTarget = target;

    #endregion
}