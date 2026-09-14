using UnityEngine;

/// <summary>
/// Base class for hierarchical player states.
/// Ensures full sub-state cleanup when switching states.
/// </summary>
public abstract class PlayerBaseState
{
    protected PlayerBaseState(PlayerController currentContext, PlayerStateFactory factory)
    {
        _ctx = currentContext;
        _factory = factory;
    }

    protected readonly PlayerController _ctx;
    protected readonly PlayerStateFactory _factory;

    protected bool _isRootState = false;

    protected PlayerBaseState _childState;
    protected PlayerBaseState _parentState;

    public PlayerBaseState ChildState => _childState;

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

    /// <summary>
    /// Switches state and guarantees full cleanup of child hierarchy.
    /// </summary>
    public void SwitchState(PlayerBaseState newState)
    {
        
        // 🔥 Clear entire sub-state tree before exiting
        ClearSubStateRecursive();

        ExitState();

        if (_isRootState)
        {
            _ctx.CurrentState = newState;
        }
        else
        {
            _parentState?.SetChildState(newState);
        }

        newState.EnterState();
    }

    /// <summary>
    /// Recursively clears child states to prevent lingering updates.
    /// </summary>
    private void ClearSubStateRecursive()
    {
        if (_childState == null)
            return;

        _childState.ClearSubStateRecursive();
        _childState.ExitState();
        _childState = null;
    }

    protected void SetChildState(PlayerBaseState newChildState)
    {
        _childState = newChildState;
        _childState.SetParentState(this);
        _childState.EnterState();
    }

    protected void SetParentState(PlayerBaseState newParentState)
    {
        _parentState = newParentState;
    }
}