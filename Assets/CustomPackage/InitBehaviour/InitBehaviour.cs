using System;
using UnityEngine;

public class InitBehaviour : MonoBehaviour
{
    private bool _isInitialized = false;
    public bool IsInitialized => _isInitialized;

    protected virtual void Start()
    {
        Initialize();
    }
    
    public virtual bool Initialize()
    {
        if (_isInitialized) return false;
        _isInitialized = true;
        return true;
    }
}
