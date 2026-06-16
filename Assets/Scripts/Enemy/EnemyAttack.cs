using System.Collections.Generic;
using UnityEngine;

public class EnemyAttack : MonoBehaviour, IAttack
{
    private Damageable _damageable;
    private EnemyController _controller;

    public int CurrentComboIndex { get; private set; } = 0;
    public bool IsAttacking { get; private set; } = false;

    private HashSet<string> _activeWindows = new HashSet<string>();
    private AttackData _currentAttackData;

    // Cờ đánh dấu đã tự động kích hoạt đòn kế tiếp trong cửa sổ hiện tại chưa
    private bool _comboExecutedInThisWindow = false;

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

        // Reset toàn bộ trạng thái window của đòn mới
        foreach (var window in _currentAttackData.windows)
            window.ResetRuntime();

        _activeWindows.Clear();

        if (_controller.Animator != null)
        {
            // Ép Animator chuyển đòn dứt khoát từ giây 0 (Animation Cancel)
            _controller.Animator.CrossFadeInFixedTime(_currentAttackData.animationName, 0.1f, 0, 0f);
        }

        Debug.Log($"[Enemy Combo] Kích hoạt đòn: {_currentAttackData.animationName} (Index: {CurrentComboIndex})");
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

        // Nếu đang blend chuyển đòn, giữ trạng thái chạy cho BT
        if (_controller.Animator.IsInTransition(0))
            return true;

        Animator stateAnimator = _controller.Animator;
        AnimatorStateInfo stateInfo = stateAnimator.GetCurrentAnimatorStateInfo(0);

        if (!stateInfo.IsName(_currentAttackData.animationName))
            return true;

        // Tính toán thời gian thực tế dựa trên Animation Speed (Giống Player)
        float animSpeed = stateInfo.speed;
        float effectiveLength = stateInfo.length / Mathf.Max(animSpeed, 0.001f);

        float nTime = stateInfo.normalizedTime;
        if (stateInfo.loop) nTime %= 1f;

        // Tự động xoay mặt về phía Player khi combo
        if (playerTransform != null)
        {
            Vector3 direction = (playerTransform.position - transform.position).normalized;
            direction.y = 0;
            if (direction != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(direction);
        }

        // QUÉT CỬA SỔ WINDOWS
        foreach (var window in _currentAttackData.windows)
        {
            if (window.IsInside(nTime))
            {
                // TỰ ĐỘNG CANCELED HOẠT ẢNH SỚM TẠI ĐÂY (GIẢ LẬP LỆNH BẤM CỦA PLAYER)
                if (window.actionName == "ComboInputBuffer" && !_comboExecutedInThisWindow)
                {
                    if (playerTransform != null)
                    {
                        float distance = Vector3.Distance(playerTransform.position, transform.position);
                        if (distance <= attackRange)
                        {
                            // Đánh dấu để không bấm lặp lại trong cùng một cửa sổ
                            _comboExecutedInThisWindow = true;

                            Debug.Log($"[Enemy Combo] Nhận diện cửa sổ ComboInputBuffer tại nTime: {nTime}. Tự động HỦY HOẠT ẢNH SỚM để nối đòn!");
                            MoveToNextComboStep();
                            return true; // Ngắt update frame cũ để sang đòn mới ngay lập tức
                        }
                    }
                }

                // Gây sát thương Hitbox
                if (window.actionName == "Hitbox" && !_activeWindows.Contains("Hitbox"))
                {
                    _activeWindows.Add("Hitbox");
                    if (playerTransform != null) Detection_NA(playerTransform.gameObject);
                }

                // Kích hoạt các hiệu ứng SO Event nếu có
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

        // BIỆN PHÁP AN TOÀN: Nếu đi hết 95% hoạt ảnh mà không thể nối combo (Player chạy mất)
        if (nTime >= 0.95f)
        {
            ExitAttackState();
            return false; // Trả về false để BT biết chuỗi combo kết thúc thất bại
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
        Debug.Log("[Enemy Combo] Chuỗi combo kết thúc hoàn toàn. Trả về Idle.");
    }

    public void Detection_NA(GameObject _gameObject)
    {
        if (_damageable == null) return;
        _damageable.CauseDMG(_gameObject, AttackType.NormalAttack);
    }
    public void Detection_CA(GameObject _gameObject) { }
    public void Detection_E(GameObject _gameObject) { }
    public void Detection_Q(GameObject _gameObject) { }
}