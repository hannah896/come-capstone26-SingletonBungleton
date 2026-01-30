using UnityEngine;

public abstract class Entity : MonoBehaviour, IPoolable {
    
    public string PoolKey { get; set; }

    private bool _isInitialized;

    void Start() {
        Initialize();
    }

    public virtual bool Initialize() {
        if (_isInitialized) return false;
        _isInitialized = true;
        
        return true;
    }

    public virtual void OnRelease() { }


    public void OnSpawn()
    {
        throw new System.NotImplementedException();
    }

    public void OnDespawn()
    {
        throw new System.NotImplementedException();
    }
}