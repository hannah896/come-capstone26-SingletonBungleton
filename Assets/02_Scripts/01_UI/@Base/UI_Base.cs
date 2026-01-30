using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
#if UNITY_EDITOR
#endif


public class UI_Base : MonoBehaviour
{
    public UnityEvent OnOpenEvent = new UnityEvent();
    public UnityEvent OnCloseEvent = new UnityEvent();

    protected bool _onClose = false;
    
    public RectTransform Rect { get; private set; }
    private bool _isInitialized;
    
    
    protected virtual void Awake() { Initialize(); }

    protected virtual void Start()
    {
        OnOpenEvent?.Invoke();
    }

    public virtual bool Initialize() {
        if (_isInitialized) return false;

        Rect = this.GetComponent<RectTransform>();

        _isInitialized = true;
        return true;
    }
    
    #region Rect

    public UI_Base SetRectAnchor(Vector2 anchorMin, Vector2 anchorMax) {
        Initialize();

        Rect.anchorMin = anchorMin;
        Rect.anchorMax = anchorMax;

        return this;
    }

    public UI_Base SetRectPivot(Vector2 pivot) {
        Initialize();

        Rect.pivot = pivot;

        return this;
    }

    public UI_Base SetRectAnchoredPosition(Vector2 position) {
        Initialize();

        Rect.anchoredPosition = position;

        return this;
    }

    public UI_Base SetSize(Vector2 size) {
        Initialize();

        Rect.sizeDelta = size;

        return this;
    }

    public UI_Base SetOffset(Vector2 offsetMin, Vector2 offsetMax) {
        Initialize();

        Rect.offsetMin = offsetMin;
        Rect.offsetMax = offsetMax;

        return this;
    }
    
    #endregion

    public virtual void Close()
    {
        if (_onClose) return;
        _onClose = true;

        OnCloseEvent?.Invoke();
    }
    #region EditorSetting
    
#if UNITY_EDITOR
    private void OnValidate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        var fields = GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (var field in fields)
        {
            // [SerializeField] 붙은 private 필드만 자동 할당
            if (field.GetCustomAttribute<SerializeField>() == null)
                continue;

            // Component 상속 타입이면 이름으로 찾기
            if (field.FieldType.IsSubclassOf(typeof(Component)))
            {
                field.SetValue(this, FindComponent(field.FieldType, field.Name));
                continue;
            }

            // GameObject 타입인 경우
            if (field.FieldType != typeof(GameObject))
                continue;

            var component = FindComponent(typeof(Transform), field.Name);
            if (component == null)
                continue;

            field.SetValue(this, component.gameObject);
        }
        
        Component FindComponent(Type type, string name)
        {
            var components = GetComponentsInChildren(type, true);
            foreach (var component in components)
            {
                if (component.name == name)
                    return component;
            }

            return null;
        }
    }
#endif
    
    #endregion
}

