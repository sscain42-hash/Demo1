public abstract class PlayerBaseState
{
    protected readonly PlayerController _ctx;
    protected readonly PlayerStateFactory _factory;

   
    protected PlayerBaseState _childState;
    protected PlayerBaseState _parentState;
    protected bool _isChildState = false;
    public PlayerBaseState ChildState => _childState;
    public bool IsRootState => !_isChildState;
    public bool IsChildState => _isChildState;



    public virtual StateCategory Category => StateCategory.Locomotion;

    protected PlayerBaseState(PlayerController ctx, PlayerStateFactory factory)
    {
        _ctx = ctx;
        _factory = factory;
    }

    public virtual void EnterState() { }
    protected virtual void UpdateState() { }
    protected virtual void ExitState() { }
    public virtual void CheckSwitchState() { }
    public virtual void InitializeSubState() { }

    public void UpdateStates()
    {
        UpdateState();
        _childState?.UpdateStates();
    }

    public void ForceExit()
    {
        ClearSubStateRecursive();
        ExitState();
    }

    public void SwitchState(PlayerBaseState newState)
    {
        if (newState == null) return;
        if (GetType() == newState.GetType()) return;

        ClearSubStateRecursive();
        ExitState();

        // ✅ Dùng newState.IsRootState — giờ là property tự detect
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

    /// <summary>
    /// 🔥 CHILD state gọi hàm này để pop về parent.
    /// Parent sẽ tự InitializeSubState lại (Idle/Run).
    /// </summary>
    public void PopToParent()
    {
        if (_parentState == null)
        {
            // Fallback: nếu không có parent, switch root về Grounded
            _ctx.SwitchStateExternal(_factory.Grounded());
            return;
        }

        var parent = _parentState;

        // Xoá child hiện tại (chính là state này)
        parent.ClearChild();

        // Parent tự init lại (Idle/Run)
        parent.InitializeSubState();
    }

    /// <summary>
    /// 🔥 Dùng nội bộ cho PopToParent. Xoá child khỏi parent.
    /// </summary>
    internal void ClearChild()
    {
        if (_childState == null) return;

        _childState.ClearSubStateRecursive();
        _childState.ExitState();
        _childState = null;
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


}