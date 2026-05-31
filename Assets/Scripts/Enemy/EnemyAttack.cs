using System.Collections.Generic;
using UnityEngine;

public class EnemyAttack : MonoBehaviour, IAttack
{
    private Damageable _damageable;
    private EnemyController _controller;
    
    public int CurrentComboIndex { get; private set; } = 0;
    public bool IsAttacking { get; private set; } = false;

    private HashSet<string> _activeWindows = new HashSet<string>();
    
    // Cờ đánh dấu để AI chỉ "bấm nút nối combo" duy nhất 1 lần khi lọt vào cửa sổ ComboInput
    private bool _comboExecutedInThisWindow = false; 

    private void Awake()
    {
        _damageable = GetComponent<Damageable>();
        _controller = GetComponent<EnemyController>();
    }

    /// <summary>
    /// Giả lập hàm EnterState của Player: Khởi tạo đòn đánh đầu tiên
    /// </summary>
    public void EnterAttackState()
    {
        if (_controller == null || _controller.ComboSeq == null || _controller.ComboSeq.attacks.Count == 0) return;

        IsAttacking = true;
        CurrentComboIndex = 0; 
        _activeWindows.Clear();
        _comboExecutedInThisWindow = false;
        _controller.SetAttackSensor(true);

        StartAttackStep();
    }

    /// <summary>
    /// Giả lập hàm StartAttackStep của Player: Ép Animator chuyển đòn dứt khoát từ giây 0
    /// </summary>
    private void StartAttackStep()
    {
        _activeWindows.Clear();
        _comboExecutedInThisWindow = false;

        AttackData currentAttack = _controller.ComboSeq.attacks[CurrentComboIndex];
        
        // Fix cứng thời gian offset là 0f để hoạt ảnh luôn bắt đầu lại từ đầu đòn mới
        _controller.Animator.CrossFadeInFixedTime(currentAttack.animationName, 0.1f, 0, 0f);
    }

    /// <summary>
    /// Giả lập hàm ExecuteCombo của Player: Tăng index hoặc reset chuỗi đòn
    /// </summary>
    private void ExecuteCombo()
    {
        _comboExecutedInThisWindow = true;

        // Nếu còn đòn tiếp theo thì tăng index, nếu hết chuỗi thì quay lại đòn 1 (Chém vĩnh viễn)
        if (CurrentComboIndex < _controller.ComboSeq.attacks.Count - 1)
        {
            CurrentComboIndex++;
        }
        else
        {
            CurrentComboIndex = 0; // Vòng lặp combo quay lại đòn đầu tiên
        }

        StartAttackStep();
    }

    /// <summary>
    /// Hàm Update quét vị trí cửa sổ tương tự PlayerAttackState
    /// </summary>
    public bool UpdateAttackState(Transform playerTransform, float attackRange)
    {
        if (!IsAttacking || _controller == null || _controller.ComboSeq == null) 
            return false;

        // Nếu Animator đang trong quá trình Blend chuyển đòn, cứ để đồ họa chạy mượt
        if (_controller.Animator.IsInTransition(0))
            return true;

        AnimatorStateInfo stateInfo = _controller.Animator.GetCurrentAnimatorStateInfo(0);
        AttackData currentData = _controller.ComboSeq.attacks[CurrentComboIndex];

        // Đảm bảo Animator đã đồng bộ xong sang tên state đòn hiện tại mới quét window
        if (!stateInfo.IsName(currentData.animationName))
            return true;

        float nTime = stateInfo.normalizedTime;
        if (stateInfo.loop) nTime %= 1f;

        // QUÉT CỬA SỔ WINDOWS (Y HỆT PLAYER)
        foreach (var window in currentData.windows)
        {
            bool isInside = window.IsInside(nTime);

            // --- SỰ KIỆN: WINDOW ENTER ---
            if (isInside && !_activeWindows.Contains(window.actionName))
            {
                _activeWindows.Add(window.actionName);

                // Nếu chạm vào cửa sổ mở ComboInput: AI tự động "bấm nút" nối đòn ngay lập tức
                if (window.actionName == "ComboInput" && !_comboExecutedInThisWindow)
                {
                    if (playerTransform != null)
                    {
                        float distance = Vector3.Distance(playerTransform.position, transform.position);
                        
                        // Miễn là Player còn sống và nằm trong tầm đánh -> Phát động lệnh ExecuteCombo ngay
                        if (distance <= attackRange)
                        {
                            ExecuteCombo();
                            return true; // Ngắt hàm luôn vì đã chuyển sang đòn mới
                        }
                    }
                }

                // Nếu chạm vào cửa sổ Hitbox: Kích hoạt gây sát thương
                if (window.actionName == "Hitbox" && playerTransform != null)
                {
                    Detection_NA(playerTransform.gameObject);
                }
            }

            // --- SỰ KIỆN: WINDOW EXIT ---
            if (!isInside && _activeWindows.Contains(window.actionName))
            {
                _activeWindows.Remove(window.actionName);
                window.ResetRuntime();
            }
        }

        // BIỆN PHÁP AN TOÀN: Nếu hoạt ảnh chạy quá cuối đòn (> 98%) mà ko thể nối combo 
        // (Do Player đã chết hoặc đã chạy thoát khỏi tầm đánh trước khi kịp mở cửa sổ ComboInput)
        if (nTime >= 0.98f)
        {
            ExitAttackState();
            return false; // Báo cho Behavior Tree biết chuỗi đòn đã đứt
        }

        return true;
    }

    /// <summary>
    /// Giả lập hàm ExitState của Player: Dọn dẹp bộ nhớ và trả về trạng thái mặc định
    /// </summary>
    public void ExitAttackState()
    {
        _activeWindows.Clear();
        IsAttacking = false;
        CurrentComboIndex = 0;
        _comboExecutedInThisWindow = false;
        
        if (_controller != null)
        {
            _controller.SetAttackSensor(false);
            
            if (_controller.Animator != null)
            {
                // Trả quái về trạng thái Idle mặc định của nó sau khi chuỗi đòn kết thúc hoàn toàn
                _controller.Animator.CrossFadeInFixedTime("Idle", 0.2f, 0, 0f);
            }
        }
    }

    public void Detection_NA(GameObject _gameObject)
    {
        if (_damageable == null) return;
        Debug.Log($"[Enemy Combo] Thực hiện đòn chém index: {CurrentComboIndex}");
        _damageable.CauseDMG(_gameObject, AttackType.NormalAttack);
    }

    public void Detection_CA(GameObject _gameObject) { if (_damageable != null) _damageable.CauseDMG(_gameObject, AttackType.ChargedAttack); }
    public void Detection_E(GameObject _gameObject) { if (_damageable != null) _damageable.CauseDMG(_gameObject, AttackType.E); }
    public void Detection_Q(GameObject _gameObject) { if (_damageable != null) _damageable.CauseDMG(_gameObject, AttackType.Q); }
}