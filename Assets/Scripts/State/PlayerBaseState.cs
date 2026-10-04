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

    public void SwitchState(PlayerBaseState newState)
    {
        if (newState == null) return;

        // Guard: không switch sang cùng state
        if (GetType() == newState.GetType()) return;

        ClearSubStateRecursive();
        ExitState();

        // Root state phải luôn thay CurrentState, bất kể state hiện tại là root hay child
        if (newState.IsRootState)
        {
            _ctx.CurrentState = newState;
            newState._parentState = null;
            newState.EnterState();
        }
        else
        {
            _parentState?.SetChildState(newState);
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
}