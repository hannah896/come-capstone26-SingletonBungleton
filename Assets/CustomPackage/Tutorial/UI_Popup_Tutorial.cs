using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class UI_Popup_Tutorial : UI_Popup
{
    #region Fields

    private Sequence _sequence;
    private Action _onUpdateAction;
    private Transform _trTarget;
    private CanvasGroup _cgMask;
    private CanvasGroup _cgCharacter;
    private CanvasGroup _cgText;
    private UI_Button _btnMask;

    #endregion

    #region MonoBehaviours

    protected void OnDisable()
    {
        _sequence?.Kill();
        _onUpdateAction = null;
        GameScene.GameState = GameState.Ready;
        // Main.Input.SetInputActions(InputActionType.GameScenePlay);
    }

    protected void Update()
    {
        _onUpdateAction?.Invoke();
    }

    #endregion

    #region Initialize / Set

    public override bool Initialize()
    {
        base.Initialize();
        _btnMask = gameObject.FindChild<UI_Button>("Btn_Mask");
        _cgMask = gameObject.FindChild<CanvasGroup>("Mask");
        _cgCharacter = gameObject.FindChild<CanvasGroup>("Img_Character");
        _cgText = gameObject.FindChild<CanvasGroup>("Img_TutorialText");

        return true;
    }

    public void Set(TutorialLevelType type)
    {
        Initialize();
        _cgText.alpha = 1;
        _cgMask.alpha = 1;
        _cgCharacter.alpha = 1;
        GameScene.GameState = GameState.InTutorial;
        // Main.Input.SetInputActions(InputActionType.None);
        
        CheckSetTutorial(type);
    }

    #endregion

    private void CheckSetTutorial(TutorialLevelType type)
    {
        if (TutorialLevelType.Tutorial1 == type) Tutorial1Start();
    }

    private void SelectObject(Transform tr, UnityAction action)
    {
        _trTarget = tr;
        _btnMask.SetEvent(action);
    }

    private void SelectTrObj()
    {
        if (_trTarget == null) return;
        _btnMask.transform.position = Main.Screen.GetScreenPos(_trTarget.position);
    }

    private void Tutorial1Start()
    {
        // Transform tr = arrow.Object.transform;
        // SelectObject(tr, () =>
        // {
        //     arrow.OnSelected();
        //     _btnMask.EventClear();
        // });
        _onUpdateAction += SelectTrObj;
    }
}

public enum TutorialLevelType
{
    None = 0,
    Tutorial1 = 1,
}