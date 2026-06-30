using NodeCanvas.BehaviourTrees;
using NodeCanvas.Framework;
using System.Collections.Generic;
using UnityEngine;

public class PetController : MonoBehaviour
{
    [System.Serializable]
    public struct PetSkillConfig
    {
        public AttackType attackType;
        [Tooltip("Thời gian hiệu lực tối đa của lệnh trong Queue")]
        public float duration;
    }

    [Header("⚔️ PET SKILL CONFIGURATION")]
    [SerializeField] private List<PetSkillConfig> skillConfigurations = new List<PetSkillConfig>();
    [SerializeField] private float defaultAttackDuration = 0.5f;
    [SerializeField] private int maxBufferSize = 10;

    private bool _isTamed = false;
    private BehaviourTreeOwner _btOwner;
    private IPlayerCombatEvents _masterCombatEvents;
    public GameObject playerObj;

    private struct PetCommand
    {
        public AttackType attackType;
        public float expireTime;

        public PetCommand(AttackType type, float duration)
        {
            attackType = type;
            expireTime = Time.time + duration;
        }
    }

    private readonly Queue<PetCommand> _petCommandBuffer = new Queue<PetCommand>();
    private AttackType? _executingAttackType = null;

    private void Awake()
    {
        _btOwner = GetComponent<BehaviourTreeOwner>();
    }
    private void Start()
    {
        OnTamed();
    }
    [ContextMenu("tame")]
    public void OnTamed()
    {
        _isTamed = true;
        if (playerObj != null)
        {
            _masterCombatEvents = playerObj.GetComponent<IPlayerCombatEvents>();
            if (_masterCombatEvents != null)
            {
                _masterCombatEvents.OnPlayerSkillCast += BufferPetSkill;
            }
        }
    }

    private void BufferPetSkill(AttackType skillType)
    {
        if (!_isTamed) return;

        float duration = GetSkillDuration(skillType);

        if (_petCommandBuffer.Count > 0 && _petCommandBuffer.Peek().attackType == skillType)
            return;

        if (_petCommandBuffer.Count >= maxBufferSize)
        {
            _petCommandBuffer.Dequeue();
        }

        _petCommandBuffer.Enqueue(new PetCommand(skillType, duration));
    }

    private void Update()
    {
        if (!_isTamed) return;

        CleanupExpiredCommands();
        ProcessPetCommandBuffer();
    }

    private void CleanupExpiredCommands()
    {
        while (_petCommandBuffer.Count > 0)
        {
            if (Time.time > _petCommandBuffer.Peek().expireTime)
                _petCommandBuffer.Dequeue();
            else
                break;
        }
    }

    private void ProcessPetCommandBuffer()
    {
        if (_btOwner == null || _btOwner.blackboard == null) return;
        IBlackboard blackboard = _btOwner.blackboard;

        if (!_executingAttackType.HasValue && _petCommandBuffer.Count > 0)
        {
            PetCommand nextCmd = _petCommandBuffer.Peek();

            switch (nextCmd.attackType)
            {
                case AttackType.NormalAttack:
                    blackboard.SetVariableValue("Normal", true);
                    break;
                case AttackType.E:
                    blackboard.SetVariableValue("E", true);
                    break;
                case AttackType.Q:
                    blackboard.SetVariableValue("Q", true);
                    break;
            }

            _btOwner.StartBehaviour();
        }
    }

    public bool ConsumeCommand(AttackType type)
    {
        if (_petCommandBuffer.Count == 0) return false;

        if (_petCommandBuffer.Peek().attackType == type)
        {
            _petCommandBuffer.Dequeue();
            _executingAttackType = type;
            return true;
        }
        return false;
    }

    public void ClearExecutingState(AttackType type)
    {
        if (_btOwner == null || _btOwner.blackboard == null) return;

        if (_executingAttackType == type)
        {
            _executingAttackType = null;

            switch (type)
            {
                case AttackType.NormalAttack:
                    _btOwner.blackboard.SetVariableValue("Normal", false);
                    break;
                case AttackType.E:
                    _btOwner.blackboard.SetVariableValue("E", false);
                    break;
                case AttackType.Q:
                    _btOwner.blackboard.SetVariableValue("Q", false);
                    break;
            }
        }
    }

    private float GetSkillDuration(AttackType type)
    {
        for (int i = 0; i < skillConfigurations.Count; i++)
        {
            if (skillConfigurations[i].attackType == type)
                return skillConfigurations[i].duration;
        }
        return defaultAttackDuration;
    }

    private void OnDestroy()
    {
        if (_masterCombatEvents != null)
        {
            _masterCombatEvents.OnPlayerSkillCast -= BufferPetSkill;
        }
    }
}