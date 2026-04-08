using UnityEngine;

public abstract class StateBase
{
    public abstract void OnEnter();
    public abstract void OnExit();
    public abstract void Update(float time = 1.0f);
    public abstract void FixedUpdate(float time = 1.0f);
}