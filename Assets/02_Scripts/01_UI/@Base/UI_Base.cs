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

