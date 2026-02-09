using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class ItemReceiver : Entity {

    #region Fields

    private static readonly Dictionary<CollectorItemType, Stack<ItemReceiver>> Receivers = new();

    [SerializeField] private CollectorItemType _type;
    [SerializeField] private float _animationDuration = 0.2f;

    private bool _isAnimating;
    private Vector3 _originScale;

    private Sequence _sequence;
    
    #endregion

    #region MonoBehaviours

    void OnEnable() {
        _originScale = this.transform.localScale;
        if (!Receivers.ContainsKey(_type)) Receivers[_type] = new();
        Receivers[_type].Push(this);
        ItemDisplay.OnItemReceived += OnItemReceived;
    }

    void OnDisable() {
        ItemDisplay.OnItemReceived -= OnItemReceived;
        _isAnimating = false;
        _sequence?.Kill();
        this.transform.localScale = _originScale;

        if (!Receivers.ContainsKey(_type)) return;
        Stack<ItemReceiver> tempStack = new();
        while (Receivers[_type].Count > 0) {
            ItemReceiver receiver = Receivers[_type].Pop();
            if (receiver != this) tempStack.Push(receiver);
        }

        while (tempStack.Count > 0) Receivers[_type].Push(tempStack.Pop());
        if (Receivers[_type].Count == 0) Receivers.Remove(_type);
    }

    #endregion
    
    #region Initialize / Set

    public override bool Initialize() {
        if (!base.Initialize()) return false;

        // _originScale = this.transform.localScale;
        // if (!Receivers.ContainsKey(_type)) Receivers[_type] = new();
        // Receivers[_type].Push(this);
        // ItemDisplay.OnItemReceived += OnItemReceived;

        return true;
    }

    #endregion

    #region Events

    private void OnItemReceived(CollectorItemType type, int amount) {
        if (type != _type) return;
        if (Receivers.TryGetValue(_type, out Stack<ItemReceiver> stack) && stack.Count > 0 && stack.Peek() != this) return;
        if (_isAnimating) return;

        _isAnimating = true;
        
        _sequence?.Kill();
        _sequence = DOTween.Sequence()
            .Append(this.transform.DOScale(_originScale * 1.1f, _animationDuration * 0.5f))
            .Append(this.transform.DOScale(_originScale, _animationDuration * 0.5f))
            .OnComplete(() => {
                _isAnimating = false;
            });
    }

    #endregion

    #region Get

    public static Vector3 GetPosition(CollectorItemType type) {
        if (Receivers.TryGetValue(type, out Stack<ItemReceiver> stack) && stack.Count > 0)
            return stack.Peek().transform.position;
        Debug.Log($"[ItemReceiver] GetPosition({type}): Not found receiver.");
        return Vector3.zero;
    }

    public static Transform GetTransform(CollectorItemType type) {
        if (Receivers.TryGetValue(type, out Stack<ItemReceiver> stack) && stack.Count > 0)
            return stack.Peek().transform;
        Debug.Log($"[ItemReceiver] GetPosition({type}): Not found receiver.");
        return null;
    }

    #endregion
    
}