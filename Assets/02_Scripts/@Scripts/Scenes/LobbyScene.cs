using Cysharp.Threading.Tasks;
using System;
using System.Collections;
using System.Collections.Generic;
using JSAM;
using System.Threading;
using UnityEngine;
using Object = UnityEngine.Object;

public class LobbyScene : SceneBase
{
    #region Properties
    
    public UI_Hud_Lobby HudLobbyUIHud { get; private set; }

    public static LobbyState LobbyState
    {
        get => _lobbyState;
        set
        {
            if (_lobbyState == value) return;
            _lobbyState = value;
            if (LobbyState == LobbyState.Ready)
            {
                (Main.Scene.Current as LobbyScene)?.OnLobbyReady?.Invoke();
            }
            else if (LobbyState == LobbyState.Start)
            {
                (Main.Scene.Current as LobbyScene)?.OnLobbyStart?.Invoke();
            }
        }
    }

    #endregion

    #region Fields
    
    // 팝업 체인 시스템
    private Queue<PopupInfo> _popupQueue = new();
    private bool _isProcessingPopups = false;
    private static LobbyState _lobbyState;

    public event Action OnLobbyReady;
    public event Action OnLobbyStart;

    #endregion

    #region Popup Info Class

    private class PopupInfo
    {
        public System.Func<bool> Condition { get; set; }
        public System.Func<UI_Popup> OpenAction { get; set; }
        public string Name { get; set; }

        public PopupInfo(string name, System.Func<bool> condition, System.Func<UI_Popup> openAction)
        {
            Name = name;
            Condition = condition;
            OpenAction = openAction;
        }
    }

    #endregion

    private void OnEnable()
    {
        LobbyState = LobbyState.Ready;
    }

    private async void InitializeLobbySequence()
    {
        // #00. Scene Audio Sounds(BGM)
        AudioManager.StopAllMusic();
        // AudioManager.PlayMusic(BgmKey.BgmLobby);
        
        // #01. UI Setup - 먼저 UI를 생성하고 초기화만
        HudLobbyUIHud = Object.FindFirstObjectByType<UI_Hud_Lobby>();
        if (!HudLobbyUIHud)
        {
            HudLobbyUIHud = await Main.Resource.LoadAssetAsync<UI_Hud_Lobby>();
        }

        // #02. UI 초기화 및 홈페이지로 강제 설정
        await InitializeUIWithHomePage();
        await UniTask.NextFrame();
        
        // #04. Sequence Start
        SequenceEntryPoint();
    }

    private async UniTask InitializeUIWithHomePage()
    {
        // UI 초기화
        HudLobbyUIHud.Initialize();
        
        //// 홈페이지로 즉시 설정 (애니메이션 없이)
        //HudLobbyUIHud.Nav.NavigateTo(PageType.Lobby, immediate: true);
        
        // 한 프레임 대기하여 UI가 완전히 설정되도록 함
        await UniTask.NextFrame();
        
        // 최종 Set 호출
        HudLobbyUIHud.Set(this);
    }

    private void SequenceEntryPoint()
    {
        PopupSequence();
        return;
        
        //남은 골드 이동 애니메이션이 있는지 확인하고 있다면 바로 팝업 실행 후 리턴.
        // var difference = _userPrefs.Gold.Value - _userPrefs.Gold.VisualValue;
        // if (difference <= 0)
        // {
        //     PopupSequence();
        //     return;
        // }
        //
        // var pageHome = LobbyUI.Page.GetPage<UI_PageHome>(ePageType.Lobby);
        // var startPosition = pageHome.StartRoot.position;
        //
        // RewardProvider.Play(RewardKey.Gold, (int)difference, 5, startPosition, PopupSequence);
    }

    #region Popup Chain System

    public void PopupSequence()
    {
        // 팝업 체인 설정 (순서 중요!)
        SetupPopupChain();
        
        // 체인 시작
        StartPopupChain();
    }

    private void SetupPopupChain()
    {
        _popupQueue.Clear();

        // 1. 평점 팝업 (레벨 조건 + 미수락)
        // _popupQueue.Enqueue(new PopupInfo(
        //     "Rating",
        //     () => !_userPrefs.IsAcceptedRating && _userPrefs.Level.Value == Constant.Level_Rating,
        //     () => Main.UI.OpenPopup<UI_Rating>()
        // ));

        // 추가 팝업들...
        // _popupQueue.Enqueue(new PopupInfo(...));
    }

    private void StartPopupChain()
    {
        if (_isProcessingPopups) return;
        
        _isProcessingPopups = true;
        ProcessNextPopup();
    }

    private void ProcessNextPopup()
    {
        // 큐가 비었으면 종료
        if (_popupQueue.Count == 0)
        {
            _isProcessingPopups = false;
            return;
        }

        var popupInfo = _popupQueue.Dequeue();
        if (!popupInfo.Condition())
        {
            ProcessNextPopup();
            return;
        }

        var popup = popupInfo.OpenAction();
        if (!popup)
        {
            ProcessNextPopup();
            return;
        }
        
        SetupPopupCloseCallback(popup);
    }

    private void SetupPopupCloseCallback(UI_Popup popup)
    {
        var originalCloseAction = popup.OnCloseEvent;
        popup.OnCloseEvent.AddListener(() =>
        {
            originalCloseAction?.Invoke();
            ProcessNextPopup();
        });
    }

    /// <summary>
    /// 체인 강제 중단
    /// </summary>
    public void StopPopupChain()
    {
        _popupQueue.Clear();
        _isProcessingPopups = false;
    }

    /// <summary>
    /// 특정 팝업만 즉시 열기 (체인 무시)
    /// </summary>
    public async void OpenPopupImmediate<T>() where T : UI_Popup
    {
        await Extensions.ShowPopup<T>();
    }

    #endregion
    
    private void OnDestroy()
    {
        if (!AudioManager.Instance || !AudioManagerInternal.Instance) return;
        
        AudioManager.StopAllMusic();
    }

    public override UniTask EnterScene(CancellationToken token)
    {
        Main.UI.HideScreen(3);
        InitializeLobbySequence();
        return UniTask.CompletedTask;
    }

    public override void ExitScene()
    {
        // 씬을 떠날 때 진행 중인 팝업 체인을 정리합니다.
        StopPopupChain();
    }
}

public enum LobbyState
{
    None = -1,
    Ready,
    Start,
}