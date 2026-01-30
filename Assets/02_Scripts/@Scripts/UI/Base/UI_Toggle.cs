using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class UI_Toggle : UI_Image, IPointerClickHandler {

    #region Properties

    public bool IsOn {
        get => _isOn;
        set {
            _isOn = value;
            OnToggled?.Invoke(value);
            SetToggleState();
        }
    }

    #endregion
    
    #region Fields

    // Inspector.
    [Header("Sprites")]
    [SerializeField] private Sprite _toggleOn;
    [SerializeField] private Sprite _toggleOff;
    
    private bool _isOn;
    private float _toggleOffset;
    private bool _isAnimating;
    
    
    // Events.
    public Action<bool> OnToggled;

    #endregion

    #region MonoBehaviours

    public void OnPointerClick(PointerEventData _) {
        if (_isAnimating) return;
        IsOn = !IsOn;
    }

    #endregion

    #region Initialize / Set

    public void Set(bool isOn) {
        Initialize();

        _isOn = isOn;
        SetToggleState();
    }

    public UI_Toggle SetEvent(Action<bool> action) {
        Initialize();

        OnToggled -= action;
        OnToggled += action;

        return this;
    }

    #endregion

    private void SetToggleState() {
        this.Sprite = IsOn ? _toggleOn : _toggleOff;
    }

}