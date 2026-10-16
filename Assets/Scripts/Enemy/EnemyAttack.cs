using UnityEngine;
using UnityEngine.AI;

public class EnemyAttack : MonoBehaviour, IComboCharacter, IDamageProvider
{
    private EnemyController _controller;
    private NavMeshAgent _agent;
    private EnemyComboEngine _comboEngine;

    public int CurrentComboIndex { get; private set; } = 0;
    public bool IsAttacking { get; private set; } = false;
    public ComboSequence CurrentComboSeq { get; private set; }

    private bool _comboExecutedInThisWindow = false;
    private bool _hasEnteredAttackAnim = false;
    private float _enterTime;
    private const float ENTER_TIMEOUT = 0.5f;
    private const float EXIT_NORMALIZED_TIME = 0.95f;

    public AttackData CurrentAttackData => _comboEngine?.CurrentAttackData;
    public AttackType CurrentRuntimeAttackType => AttackType.NormalAttack;

    private void Awake()
    {
        _controller = GetComponent<EnemyController>();
        _agent = GetComponent<NavMeshAgent>();
        Animator anim = GetComponentInChildren<Animator>();
        _comboEngine = new EnemyComboEngine(_controller, anim);

        // Hook parry stagger event
        _comboEngine.OnParriedByTarget += HandleParriedByPlayer;
    }

    private void OnDestroy()
    {
        if (_comboEngine != null)
            _comboEngine.OnParriedByTarget -= HandleParriedByPlayer;
    }

    /// <summary>
    /// Khi enemy bị parry → stagger (tùy chọn)
    /// </summary>
    private void HandleParriedByPlayer(GameObject parrier)
    {
        Debug.Log($"[EnemyAttack] {gameObject.name} bị PARRY — stagger!");

        // Tùy chọn: interrupt animation của enemy
        // ExitAttackState();
        // _controller.Animator.CrossFadeInFixedTime("Stagger", 0.05f, 0, 0f);
    }

    public void EnterAttackState(ComboSequence targetCombo)
    {
        if (targetCombo == null || targetCombo.attacks.Count == 0) return;

        CurrentComboSeq = targetCombo;
        IsAttacking = true;
        CurrentComboIndex = 0;
        _comboExecutedInThisWindow = false;
        _hasEnteredAttackAnim = false;
        _enterTime = Time.time;

        if (_agent != null && _agent.gameObject.activeInHierarchy)
        {
            _agent.ResetPath();
            _agent.velocity = Vector3.zero;
            _agent.isStopped = true;
            _agent.updatePosition = false;
            _agent.enabled = false;
        }

        if (_controller.CharacterController != null)
            _controller.CharacterController.enabled = true;

        ExecuteComboStep();
    }

    private void ExecuteComboStep()
    {
        _comboExecutedInThisWindow = false;
        _hasEnteredAttackAnim = false;
        _enterTime = Time.time;

        if (CurrentComboSeq != null && CurrentComboIndex < CurrentComboSeq.attacks.Count)
        {
            _comboEngine.ChangeAttackData(CurrentComboSeq.attacks[CurrentComboIndex], CurrentRuntimeAttackType);
        }
    }

    private void MoveToNextComboStep()
    {
        CurrentComboIndex++;
        ExecuteComboStep();
    }

    public bool UpdateAttackState(Transform targetTransform)
    {
        if (!IsAttacking || CurrentComboSeq == null || CurrentAttackData == null)
            return false;

        if (!_hasEnteredAttackAnim)
        {
            if (IsAnimatorInAttackState())
            {
                _hasEnteredAttackAnim = true;
            }
            else
            {
                if (Time.time - _enterTime > ENTER_TIMEOUT)
                {
                    Debug.LogWarning($"[EnemyAttack] Timeout vào attack anim '{CurrentAttackData.animationName}' — exit");
                    ExitAttackState();
                    return false;
                }

                _controller.SetCurrentTarget(targetTransform);
                return true;
            }
        }

        if (!IsAnimatorInAttackState())
        {
            ExitAttackState();
            return false;
        }

        _controller.SetCurrentTarget(targetTransform);
        Vector3 targetPos = transform.forward;

        if (targetTransform != null)
        {
            Vector3 direction = (targetTransform.position - transform.position).normalized;
            direction.y = 0;
            if (direction != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(direction);
            targetPos = targetTransform.position;
        }

        _comboEngine.UpdateWindows();

        if (_controller.Animator.IsInTransition(0))
            return true;

        float normalizedTime = _comboEngine.GetNormalizedTime();
        bool isLastAttackNode = (CurrentComboIndex >= CurrentComboSeq.attacks.Count - 1);

        if (!isLastAttackNode && _comboEngine.IsComboWindowActive && !_comboExecutedInThisWindow)
        {
            _comboExecutedInThisWindow = true;
            MoveToNextComboStep();
            return true;
        }

        if (_comboEngine.CurrentStepDisplacement != Vector3.zero)
        {
            if (_controller.CharacterController != null && _controller.CharacterController.enabled)
                _controller.CharacterController.Move(_comboEngine.CurrentStepDisplacement);
            else
                transform.position += _comboEngine.CurrentStepDisplacement;
        }

        if (isLastAttackNode)
        {
            var bufferWindow = CurrentAttackData.windows.Find(w => w.actionName == "ComboInputBuffer");
            if (bufferWindow != null && normalizedTime >= bufferWindow.startTime)
            {
                ExitAttackState();
                return false;
            }
            if (normalizedTime >= EXIT_NORMALIZED_TIME)
            {
                ExitAttackState();
                return false;
            }
        }
        else
        {
            if (normalizedTime >= EXIT_NORMALIZED_TIME)
            {
                ExitAttackState();
                return false;
            }
        }

        return true;
    }

    private bool IsAnimatorInAttackState()
    {
        if (_controller.Animator == null || CurrentAttackData == null) return false;

        if (_controller.Animator.IsInTransition(0))
        {
            var next = _controller.Animator.GetNextAnimatorStateInfo(0);
            return next.IsName(CurrentAttackData.animationName);
        }

        var current = _controller.Animator.GetCurrentAnimatorStateInfo(0);
        return current.IsName(CurrentAttackData.animationName);
    }

    public void ExitAttackState()
    {
        if (!IsAttacking) return;

        IsAttacking = false;
        CurrentComboIndex = 0;
        _comboExecutedInThisWindow = false;
        _hasEnteredAttackAnim = false;
        CurrentComboSeq = null;

        if (_controller.CharacterController != null)
            _controller.CharacterController.enabled = false;

        if (_agent != null && _agent.gameObject.activeInHierarchy)
        {
            _agent.enabled = true;
            _agent.updatePosition = true;
            _agent.isStopped = false;
            _agent.velocity = Vector3.zero;
            _agent.Warp(transform.position);
        }

        if (_controller != null && _controller.Animator != null)
        {
            _controller.Animator.CrossFadeInFixedTime("HumanM@Idle01", 0.15f, 0, 0f);
        }
    }

    public void ExecuteDamage(GameObject victim, AttackType attackType)
    {
        _controller.CauseDMG(victim, attackType);
       Debug.LogWarning($"[EnemyAttack] {gameObject.name} gây sát thương cho {victim.name} bằng {attackType}");

    }
}