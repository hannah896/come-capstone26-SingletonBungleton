using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class UI_Button : UI_Image {

    #region Properties
    public UI_Image ButtonImage => this;

    #endregion

    #region Fields
    
    private Vector3 _defaultScale;
    
    // Components.
    private UI_Text[] _contextTexts;
    private UI_Image[] _contextImages;
    
    protected Button _button;
    
    // Events.
    public event Action OnButtonDown;
    public event Action OnButtonUp;
    
    #endregion
    
    #region Initialize / Set

    public override bool Initialize() {
        if (!base.Initialize()) return false;

        _button = GetComponent<Button>();
        _contextTexts = GetComponentsInChildren<UI_Text>();
        _contextImages = GetComponentsInChildren<UI_Image>();
        _defaultScale = transform.localScale;
        
        return true;
    }

    public UI_Button SetEvent(UnityAction action) {
        Initialize();

        if (_button == null) return this;
        
        _button.onClick.RemoveListener(action);
        _button.onClick.AddListener(action);

        return this;
    }

    public UI_Button EventClear()
    {
        Initialize();
        if (_button == null) return this;
        _button.onClick.RemoveAllListeners();
        return this;
    }

    public void SetDownUpButton() {
        EventTrigger t = gameObject.GetOrAddComponent<EventTrigger>();
        EventTrigger.Entry down = new() { eventID = EventTriggerType.PointerDown };
        down.callback.AddListener(d => OnButtonDown?.Invoke());
        t.triggers.Add(down);
        EventTrigger.Entry up = new() { eventID = EventTriggerType.PointerUp };
        up.callback.AddListener(d => OnButtonUp?.Invoke());
        t.triggers.Add(up);
    }

    public UI_Button SetActive(bool active, bool setColor = true) {
        if (active) {
            _button.interactable = true;
            if (setColor) {
                this.SetColor(Color.white);
                foreach (UI_Text text in _contextTexts)
                {
                    text.SetColor(Color.white);
                }

                foreach (UI_Image image in _contextImages)
                {
                    image.SetColor(Color.white);
                }
            }
        }
        else {
            _button.interactable = false;
            if (setColor) {
                this.SetColor(Styles.BUTTONCOLOR_DISABLED);
                foreach (UI_Text text in _contextTexts)
                {
                    text.SetColor(Styles.BUTTONCOLOR_DISABLED);
                }

                foreach (UI_Image image in _contextImages)
                {
                    image.SetColor(Styles.BUTTONCOLOR_DISABLED);
                }
            }
        }

        return this;
    }

    #endregion
    
}