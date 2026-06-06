using UnityEngine;

public interface ITargetProvider
{
    Transform GetCurrentTarget();
    bool IsLocked { get; }
}