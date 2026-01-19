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
    protected const float ANIM_DURATION = 0.2f;

    protected virtual void Start()
    {
        OnOpenEvent?.Invoke();
    }

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
            // [SerializeField] 붙은 private 필드만 대상
            if (field.GetCustomAttribute<SerializeField>() == null)
                continue;

            // 1. Component 상속 타입 처리
            if (field.FieldType.IsSubclassOf(typeof(Component)))
            {
                var found = FindComponent(field.FieldType, field.Name);
                if (found != null) // null이 아닐 때만 할당
                {
                    field.SetValue(this, found);
                }
                continue;
            }

            // 2. GameObject 타입 처리
            if (field.FieldType == typeof(GameObject))
            {
                var found = FindComponent(typeof(Transform), field.Name);
                if (found != null) // null이 아닐 때만 할당
                {
                    field.SetValue(this, found.gameObject);
                }
            }
        }
    }

    private Component FindComponent(Type type, string name)
    {
        var components = GetComponentsInChildren(type, true);
        foreach (var component in components)
        {
            if (component.name == name)
                return component;
        }

        return null;
    }
#endif
    #endregion
}

