using UnityEngine;

public abstract class PlayerBaseState
{
    protected readonly PlayerController _ctx;
    protected readonly PlayerStateFactory _factory;

    protected bool _isRootState = false;
    protected PlayerBaseState _childState;
    protected PlayerBaseState _parentState;

    public PlayerBaseState ChildState => _childState;
    public bool IsRootState => _isRootState;
    // 🔥 THÊM
    /// <summary>Category của state. Default = Locomotion.</summary>
    public virtual StateCategory Category => StateCategory.Locomotion;
    protected PlayerBaseState(PlayerController ctx, PlayerStateFactory factory)
    {
        _ctx = ctx;
        _factory = factory;
    }

    public virtual void EnterState() { }
    protected virtual void UpdateState() { }
    protected virtual void ExitState() { }   // ← GIỮ protected
    public virtual void CheckSwitchState() { }
    public virtual void InitializeSubState() { }

    public void UpdateStates()
    {
        UpdateState();
        _childState?.UpdateStates();
    }

    /// <summary>
    /// 🔥 PUBLIC API — DÀNH RIÊNG CHO PlayerController.
    /// Controller gọi hàm này để force state hiện tại thoát,
    /// trước khi gán state mới (Hit/Parry/Dead...).
    /// 
    /// ⚠️ KHÔNG dùng hàm này từ bên trong state khác — chỉ Controller.
    /// </summary>
    public void ForceExit()
    {
        ClearSubStateRecursive();
        ExitState();
    }

    /// <summary>
    /// Dùng nội bộ cho child state trong cùng root (Grounded → Idle/Run).
    /// </summary>
    public void SwitchState(PlayerBaseState newState)
    {
        if (newState == null) return;
        if (GetType() == newState.GetType()) return;

        ClearSubStateRecursive();
        ExitState();

        if (newState.IsRootState)
        {
            _ctx.CurrentState = newState;
            newState._parentState = null;
            newState.EnterState();
        }
        else if (_parentState != null)
        {
            _parentState.SetChildState(newState);
        }
        else
        {
            SetChildState(newState);
        }
    }

    private void ClearSubStateRecursive()
    {
        if (_childState == null) return;

        _childState.ClearSubStateRecursive();
        _childState.ExitState();
        _childState = null;
    }

    public void SetChildState(PlayerBaseState newChildState)
    {
        if (newChildState == null) return;

        _childState = newChildState;
        _childState._parentState = this;
        _childState.EnterState();
    }

    protected void SwitchRootState(PlayerBaseState newRootState)
    {
        if (_isRootState)
            SwitchState(newRootState);
        else
            _parentState?.SwitchRootState(newRootState);
    }
    /// <summary>
    /// Child state gọi hàm này để pop về parent.
    /// Parent sẽ tự InitializeSubState lại.
    /// </summary>
    protected void PopToParent()
    {
        if (_parentState == null)
        {
            Debug.LogWarning($"[{GetType().Name}] PopToParent nhưng không có parent!");
            _ctx.SwitchStateExternal(_factory.Grounded());
            return;
        }

        // Báo parent reset child
        var parent = _parentState;

        // Xoá child khỏi parent
        parent.ClearChild();

        // Parent tự initialize lại sub state
        parent.InitializeSubState();
    }

    /// <summary>Dùng nội bộ cho PopToParent.</summary>
    internal void ClearChild()
    {
        if (_childState == null) return;

        _childState.ClearSubStateRecursive();
        _childState.ExitState();
        _childState = null;
    }
}
