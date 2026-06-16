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
   
    private readonly List<int> _levelTable =
        new() { 11,21,31,41,51,61,71,81,91,101 };

    #endregion
    [Header("Combo Configuration")]
    [SerializeField] private ComboSequence comboSequence;

    public Animator Animator { get => _animator; private set => _animator = value; }
    public ComboSequence ComboSeq => comboSequence;

  

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
        //_player.OnDieEvent += HandlePlayerDie;
        Health.OnDieEvent += HandleDeath;
    }

    private void UnregisterEvents()
    {
        if (_player != null)
         //   _player.OnDieEvent -= HandlePlayerDie;

        Health.OnDieEvent -= HandleDeath;
    }

    #endregion


    #region === Scaling System ===

   

    private void ScaleStat(int playerValue, float ratio, Action<int> setter)
    {
        setter.Invoke(Mathf.RoundToInt(playerValue * ratio));
    }

    #endregion


    #region === Initialization ===

    private void InitializeStats()
    {
        int maxHP = RuntimeConfig.GetHP();
        Health.InitValue(maxHP, maxHP);

        SetWalkSpeed(RuntimeConfig.GetWalkSpeed());
        SetRunSpeed(RuntimeConfig.GetRunSpeed());
        SetCDNormalAttack(RuntimeConfig.GetNormalAttackCD());
        SetCDSkillAttack(RuntimeConfig.GetSkillAttackCD());
        SetCDSpecialAttack(RuntimeConfig.GetSpecialAttackCD());
    }

    private void SyncBlackboardBase()
    {
        Blackboard.SetVariableValue("Player", _player.gameObject);
        Blackboard.SetVariableValue("RootPosition", transform.position);
      
        SetDie(false);
        SetTakeDMG(false);
        SetChaseSensor(false);
        SetAttackSensor(false);
    }

    #endregion


    #region === Combat System ===

    public override void CauseDMG(GameObject target, AttackType type)
    {
        if (!DamageableData.Contains(target, out var receiver)) return;

        float percent = GetPercentByType(type);
        int damage = CalculateDamage(percent, out bool isCrit);
       
        receiver.TakeDMG(damage, isCrit);
        DMGPopUpGenerator.Instance.Create(
            target.transform.position,
            damage,
            isCrit,
            false);
        Debug.Log($"[EnemyController] '{name}' caused {damage} damage to '{target.name}' with attack type '{type}' (CRIT: {isCrit}).");
    }

    private float GetPercentByType(AttackType type)
    {
        return type switch
        {
          
            _ => 1
        };
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

        int defenderDEF = 0;
        if (_player == null)
        {
            Debug.LogWarning($"[EnemyController] _player is null when calculating damage for '{name}'. Using DEF=0 fallback.");
        }
        else if (_player.PlayerConfig == null)
        {
            Debug.LogWarning($"[EnemyController] PlayerConfig is null on player when calculating damage for '{name}'. Using DEF=0 fallback.");
        }
        else
        {
            defenderDEF = _player.PlayerConfig.GetDEF();
        }

        int minATK = defenderDEF + Random.Range(10, Mathf.Max(11, baseATK / 2));
        return Mathf.Max(minATK, scaled);
    }

    public override void TakeDMG(int damage, bool isCRIT)
    {
        float def = isCRIT
            ? Random.Range(0, RuntimeConfig.GetDEF() * 0.5f)
            : RuntimeConfig.GetDEF();

        int finalDamage = Mathf.Max(0, damage - (int)def);

        Health.Decreases(finalDamage);

        DMGPopUpGenerator.Instance.Create(
            transform.position,
            finalDamage,
            isCRIT,
            true);
    
        SetTakeDMG(true);
        OnTakeDamageEvent?.Invoke(this);
    }

    #endregion


    #region === Death ===

    private void HandleDeath()
    {
        gameObject.SetObjectLayer(ignoreLayer.value);

   

        SetDie(true);
        SetChaseSensor(false);
        SetAttackSensor(false);
        SetTakeDMG(false);

        OnDieEvent?.Invoke(this);
    }

    private void HandlePlayerDie()
    {
        SetChaseSensor(false);
        SetAttackSensor(false);
    }

    #endregion


    #region === Multipliers ===

    public void SetAttackCount(int value) => _attackCount = value;

    

    private int FindLevelIndex()
    {
        for (int i = 0; i < _levelTable.Count; i++)
            if (RuntimeConfig.GetLevel() < _levelTable[i])
                return i;

        return _levelTable.Count - 1;
    }

    #endregion


    #region === Blackboard Setters ===

    public void SetRootSensor(bool v) => Blackboard.SetVariableValue("RootSensor", v);
    public void SetChaseSensor(bool v) => Blackboard.SetVariableValue("ChaseSensor", v);
    public void SetAttackSensor(bool v) => Blackboard.SetVariableValue("AttackSensor", v);
    public void SetTakeDMG(bool v) => Blackboard.SetVariableValue("TakeDMG", v);
    public void SetDie(bool v) => Blackboard.SetVariableValue("Die", v);

    private void SetWalkSpeed(float v) => Blackboard.SetVariableValue("WalkSpeed", v);
    private void SetRunSpeed(float v) => Blackboard.SetVariableValue("RunSpeed", v);
    private void SetCDNormalAttack(float v) => Blackboard.SetVariableValue("NormalAttackCD", v);
    private void SetCDSkillAttack(float v) => Blackboard.SetVariableValue("SkillAttackCD", v);
    private void SetCDSpecialAttack(float v) => Blackboard.SetVariableValue("SpecialAttackCD", v);

    #endregion


    #region === Pool ===

    public void Release() => ReleaseCallback?.Invoke(this);

    private Coroutine _flinchRoutine;
    private Vector3 _originalLocalPos;
    private Animator _animator;

   
   

    #region === Animation CrossFade ===

    /// <summary>
    /// Kích hoạt chuyển cảnh mượt mà sang trạng thái chỉ định bằng thời gian cố định.
    /// </summary>
    /// <param name="stateName">Tên chính xác của State trong Animator Controller</param>
    /// <param name="fixedTransitionDuration">Thời gian hòa trộn giữa 2 animation (giây). Mặc định là 0.15s</param>
    public void PlayAnimationCrossFade(string stateName, float fixedTransitionDuration = 0.15f)
    {
        if (_animator != null)
        {
            // Tham số thứ 2 là thời gian chuyển cảnh (giây), tham số thứ 3 là Layer Index (mặc định là 0)
            _animator.CrossFadeInFixedTime(stateName, fixedTransitionDuration, 0);
        }
    }

    #endregion

    public Action<EnemyController> ReleaseCallback { get; set; }

    #endregion
}
