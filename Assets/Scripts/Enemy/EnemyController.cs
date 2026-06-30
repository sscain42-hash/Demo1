using System;
using System.Collections;
using System.Collections.Generic;
using NaughtyAttributes;
using NodeCanvas.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Events;
using Random = UnityEngine.Random;

public class EnemyController : Damageable, IPooled<EnemyController>
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
    private EnemyAttack enemyAttack;

    #endregion

    public Animator Animator { get => _animator; private set => _animator = value; }

    #region === Unity Lifecycle ===

    private void Awake()
    {
        Health = new StatusHandle();
        RuntimeConfig = Instantiate(baseConfig);
        _originalLocalPos = transform.localPosition;
        _animator = GetComponent<Animator>();
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

        enemyAttack = GetComponent<EnemyAttack>();
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

        int damage = CalculateDamage(RuntimeConfig.GetATK(), out bool isCrit);

        receiver.TakeDMG(damage, isCrit);

        DMGPopUpGenerator.Instance.Create(transform.position, damage, isCrit, true);
        Debug.LogError($"[EnemyController] '{name}' caused {damage} damage to '{target.name}' with attack type '{type}' (CRIT: {isCrit}).");
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

    #region === Animation CrossFade ===

    public void PlayAnimationCrossFade(string stateName, float fixedTransitionDuration = 0.15f)
    {
        if (_animator != null)
        {
            _animator.CrossFadeInFixedTime(stateName, fixedTransitionDuration, 0);
        }
    }

    #endregion

    public Action<EnemyController> ReleaseCallback { get; set; }

    #endregion
}