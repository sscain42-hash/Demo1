using System.Collections.Generic;
using UnityEngine;

public class EnemyAttack : MonoBehaviour, IAttack, IComboCharacter,IDamageProvider
{
    private Damageable _damageable;
    private EnemyController _controller;

    public int CurrentComboIndex { get; private set; } = 0;
    public bool IsAttacking { get; private set; } = false;

    private HashSet<string> _activeWindows = new HashSet<string>();
    private AttackData _currentAttackData;
    private bool _comboExecutedInThisWindow = false;

    // --- THỰC THI INTERFACE ICOMBOCHARACTER CHO ENEMY ---
    public AttackData CurrentAttackData => _currentAttackData;
    public AttackType CurrentRuntimeAttackType => AttackType.NormalAttack; // Mặc định quái đánh thường, bạn có thể đổi theo trạng thái quái nếu cần

    private void Awake()
    {
        _damageable = GetComponent<Damageable>();
        _controller = GetComponent<EnemyController>();
    }

    public void EnterAttackState()
    {
        if (_controller == null || _controller.ComboSeq == null || _controller.ComboSeq.attacks.Count == 0) return;

        IsAttacking = true;
        CurrentComboIndex = 0;
        _activeWindows.Clear();
        _comboExecutedInThisWindow = false;
        _controller.SetAttackSensor(true);

        ExecuteComboStep();
    }

    private void ExecuteComboStep()
    {
        _comboExecutedInThisWindow = false;
        _currentAttackData = _controller.ComboSeq.attacks[CurrentComboIndex];

        // Ép tất cả các ô cửa sổ reset lại cờ hiệu để sẵn sàng kích hoạt hiệu ứng mới
        if (_currentAttackData != null && _currentAttackData.windows != null)
        {
            for (int i = 0; i < _currentAttackData.windows.Count; i++)
            {
                _currentAttackData.windows[i].eventTriggered = false;
                _currentAttackData.windows[i].ResetRuntime();
            }
        }

        _activeWindows.Clear();

        if (_controller.Animator != null)
        {
            _controller.Animator.CrossFadeInFixedTime(_currentAttackData.animationName, 0.1f, 0, 0f);
        }
        Debug.Log($"[Enemy Combo] Phát động: {_currentAttackData.animationName} (Index: {CurrentComboIndex})");
    }

    private void MoveToNextComboStep()
    {
        CurrentComboIndex = (CurrentComboIndex + 1) % _controller.ComboSeq.attacks.Count;
        ExecuteComboStep();
    }

    public bool UpdateAttackState(Transform playerTransform, float attackRange)
    {
        if (!IsAttacking || _controller == null || _controller.ComboSeq == null || _currentAttackData == null)
            return false;

        if (_controller.Animator.IsInTransition(0))
            return true;

        AnimatorStateInfo stateInfo = _controller.Animator.GetCurrentAnimatorStateInfo(0);

        if (!stateInfo.IsName(_currentAttackData.animationName))
            return true;

        float animSpeed = stateInfo.speed;
        float effectiveLength = stateInfo.length / Mathf.Max(animSpeed, 0.001f);

        float nTime = stateInfo.normalizedTime;
        if (stateInfo.loop) nTime %= 1f;

        if (playerTransform != null)
        {
            Vector3 direction = (playerTransform.position - transform.position).normalized;
            direction.y = 0;
            if (direction != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(direction);
        }

        // VÒNG LẶP QUÉT CỬA SỔ WINDOWS (ĐỒNG BỘ 100% VỚI PLAYER)
        foreach (var window in _currentAttackData.windows)
        {
            if (window.IsInside(nTime))
            {
                // GIẢ LẬP INPUT CỦA PLAYER BẰNG KHOẢNG CÁCH:
                if (window.actionName == "ComboInputBuffer" && !_comboExecutedInThisWindow)
                {
                    if (playerTransform != null)
                    {
                        float distance = Vector3.Distance(playerTransform.position, transform.position);
                        if (distance <= attackRange)
                        {
                            _comboExecutedInThisWindow = true; // Đánh dấu đã "bấm nút"

                            Debug.Log($"[Enemy] Nhận diện cửa sổ ComboInputBuffer. Tự động HỦY HOẠT ẢNH SỚM để nối đòn!");
                            MoveToNextComboStep();
                            return true;
                        }
                    }
                }

                if (window.actionName == "Hitbox" && !_activeWindows.Contains("Hitbox"))
                {
                    _activeWindows.Add("Hitbox");
                    if (playerTransform != null) Detection_NA(playerTransform.gameObject);
                }

                // KÍCH HOẠT HIỆU ỨNG: Bây giờ caster truyền vào là Enemy, hàm Trigger vẫn chạy mượt!
                if (window.eventEffects != null && !window.eventTriggered)
                {
                    window.eventTriggered = true;
                    foreach (var effect in window.eventEffects)
                        effect?.Trigger(gameObject, window);
                }
            }
            else
            {
                if (window.actionName == "Hitbox" && _activeWindows.Contains("Hitbox"))
                {
                    _activeWindows.Remove("Hitbox");
                }
            }
        }

        if (nTime >= 0.95f)
        {
            ExitAttackState();
            return false;
        }

        return true;
    }

    public void ExitAttackState()
    {
        _activeWindows.Clear();
        IsAttacking = false;
        CurrentComboIndex = 0;
        _comboExecutedInThisWindow = false;
        _currentAttackData = null;

        if (_controller != null)
        {
            _controller.SetAttackSensor(false);
            if (_controller.Animator != null)
            {
                _controller.Animator.CrossFadeInFixedTime("Idle", 0.2f, 0, 0f);
            }
        }
    }

    public void Detection_NA(GameObject _gameObject)
    {
        if (_damageable == null) return;
        _damageable.CauseDMG(_gameObject, AttackType.NormalAttack);
    }
    public void Detection_CA(GameObject _gameObject) { }
    public void Detection_E(GameObject _gameObject) { }
    public void Detection_Q(GameObject _gameObject) { }

    public void ExecuteDamage(GameObject victim, AttackType attackType) => _controller.CauseDMG(victim, attackType);
}