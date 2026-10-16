using System;
using System.Collections;
using System.Collections.Generic;
using NaughtyAttributes;
using NodeCanvas.Framework;
using UnityEngine;
using UnityEngine.Events;
using Random = UnityEngine.Random;

public class EnemyController : CharacterActor,
                                IPooled<EnemyController>
{
    #region === Serialized ===

    [field: SerializeField, Required]
    public Blackboard Blackboard { get; private set; }

    [Header("Layer")]
    [SerializeField] private LayerMask mainLayer;
    [SerializeField] private LayerMask ignoreLayer;

    #endregion

    #region === Public State ===

    public SO_EnemyConfiguration EnemyConfig => RuntimeConfig as SO_EnemyConfiguration;

    public UnityEvent<EnemyController> OnTakeDamageUnityEvent;
    public UnityEvent<EnemyController> OnDieUnityEvent;

    #endregion

    #region === Private Fields ===

    private PlayerController _player;
    private int _attackCount;
    private EnemyAttack _enemyAttackRef;

    #endregion

    #region === Unity Lifecycle ===

    private CharacterController _characterController;

    protected override void Awake()
    {
        base.Awake(); // CharacterActor.Awake

        _enemyAttackRef = GetComponent<EnemyAttack>();
        _characterController = GetComponent<CharacterController>();
        if (_characterController != null)
            _characterController.enabled = false;
    }

    protected override void OnEnable()
    {
        base.OnEnable(); // Register Damageable + Init Health

        ResolveDependencies();
        RegisterEvents();
        SyncBlackboardBase();
    }

    protected override void OnDisable()
    {
        UnregisterEvents();
        base.OnDisable();
    }

    #endregion

    #region === Dependency ===

    private void ResolveDependencies()
    {
        gameObject.SetObjectLayer(mainLayer.value);

        _player = FindAnyObjectByType<PlayerController>();
        if (_player == null)
        {
            Debug.LogError($"[EnemyController] PlayerController not found for enemy '{name}'.");
        }

        _enemyAttackRef = GetComponent<EnemyAttack>();
    }

    private void RegisterEvents()
    {
        Health.OnDieEvent += HandleEnemyDeath;
        Health.OnCurrentValueChangeEvent += HandleHealthChanged;

        OnTakeDamageEvent += HandleTakeDamageEvent;
        OnDieEvent += HandleDieEvent;
    }

    private void UnregisterEvents()
    {
        Health.OnDieEvent -= HandleEnemyDeath;
        Health.OnCurrentValueChangeEvent -= HandleHealthChanged;

        OnTakeDamageEvent -= HandleTakeDamageEvent;
        OnDieEvent -= HandleDieEvent;
    }

    #endregion

    #region === Abstract Overrides ===

    public override bool IsPlayer => false;

    public override Vector3 GetLookDirection()
    {
        if (_currentTarget != null)
        {
            Vector3 dir = _currentTarget.position - transform.position;
            dir.y = 0;
            if (dir.sqrMagnitude > 0.001f)
                return dir.normalized;
        }

        return Model != null ? Model.forward : transform.forward;
    }

    public override Transform GetAttackTarget() => _currentTarget;

    protected override ComboEngineBase CreateComboEngine()
        => new EnemyComboEngine(this, Animator);

    protected override float GetDamageMultiplier(AttackType type) => 100f;

    protected override void HandleDeath()
    {
        // Base: invoke OnDieEvent + SetActive(false)
        base.HandleDeath();
    }

    #endregion

    #region === Combat System ===

    /// <summary>
    /// Override CharacterActor.CalculateDamage để dùng config riêng của Enemy.
    /// </summary>
    protected override int CalculateDamage(AttackType type)
    {
        if (RuntimeConfig == null) return 0;

        int baseATK = RuntimeConfig.GetATK();
        int scaled = Mathf.CeilToInt(baseATK * (GetDamageMultiplier(type) / 100f));

        bool isCrit = RuntimeConfig.IsCRIT;
        if (isCrit)
        {
            float critMulti = 1 + RuntimeConfig.GetCRITDMG() / 100f;
            scaled = Mathf.CeilToInt(scaled * critMulti);
        }

        return scaled;
    }

    #endregion

    #region === Death ===

    private void HandleEnemyDeath()
    {
        // Chỉ gọi 1 lần
        gameObject.SetObjectLayer(ignoreLayer.value);
        SetDie(true);
        SetTakeDMG(false);
    }

    private void HandleTakeDamageEvent(CharacterActor actor)
    {
        SetTakeDMG(true);
    }

    private void HandleDieEvent(CharacterActor actor)
    {
        OnDieUnityEvent?.Invoke(this);
    }

    private void HandleHealthChanged(int newValue)
    {
        // Có thể hook UI hoặc blackboard
        if (newValue <= 0)
        {
            // Đã trigger OnDieEvent ở StatusHandle
        }
    }

    #endregion

    #region === Blackboard Setters ===

    public void SetTakeDMG(bool v) => Blackboard.SetVariableValue("TakeDMG", v);
    public void SetDie(bool v) => Blackboard.SetVariableValue("Die", v);
    private void SetWalkSpeed(float v) => Blackboard.SetVariableValue("WalkSpeed", v);
    private void SetRunSpeed(float v) => Blackboard.SetVariableValue("RunSpeed", v);

    private void SyncBlackboardBase()
    {
        Blackboard.SetVariableValue("RootPosition", transform.position);
        SetDie(false);
        SetTakeDMG(false);

        if (RuntimeConfig != null)
        {
            SetWalkSpeed(RuntimeConfig.GetWalkSpeed());
            SetRunSpeed(RuntimeConfig.GetRunSpeed());
        }
    }

    #endregion

    #region === Pool ===

    public void Release() => ReleaseCallback?.Invoke(this);
    public Action<EnemyController> ReleaseCallback { get; set; }

    #endregion

    #region === Multipliers ===

    public void SetAttackCount(int value) => _attackCount = value;

    #endregion

    #region === Target / Reference ===

    internal CharacterController CharacterController => _characterController;

    public Transform CurrentTarget { get; private set; }
    public void SetCurrentTarget(Transform target) => CurrentTarget = target;

    // Alias cho code cũ gọi _controller.CurrentTarget
    private Transform _currentTarget => CurrentTarget;

    #endregion

    #region === EnemyAttack accessor (giữ API cũ) ===


    #endregion
}
