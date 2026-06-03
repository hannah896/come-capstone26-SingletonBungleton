using System;
using Blossom.Preference;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 앱 시작 시 표시되는 로딩 화면.
/// SDK 초기화와 최소 로딩 시간을 대기하며 프로그레스 바를 갱신합니다.
/// 로딩 완료 후 FadeOut과 함께 씬 전환을 수행합니다.
/// </summary>
public class UI_Screen_StartLoading : UI_Screen
{
    #region Constants

    private const float LoadingTime = 5f;
    private const float FadeInDuration = 0.5f;
    private const float FadeOutDuration = 0.5f;

    #endregion

    #region Fields

    private UI_Image _imgLoadingBar;
    private CanvasGroup _cg;
    private PlayPrefs _playPrefs;
    private float _showTime;

    #endregion

    #region Initialization

    public override bool Initialize()
    {
        if (!base.Initialize()) return false;

        _imgLoadingBar = gameObject.FindChild<UI_Image>("Img_Bar_F");
        _imgLoadingBar.SetFill(0);
        _cg = gameObject.GetOrAddComponent<CanvasGroup>();

        return true;
    }

    #endregion

    #region UI_Screen Overrides

    public override void Set(Action onLoadingComplete = null)
    {
        base.Set(onLoadingComplete);
        _playPrefs = Prefs.Get<PlayPrefs>();
        _showTime = 0;
        _cg.alpha = 0f;
        _imgLoadingBar.SetFill(0);
    }

    public override void FadeInLoading()
    {
        gameObject.SetActive(true);
        sequence?.Kill();
        sequence = DOTween.Sequence();
        sequence.Append(_cg.DOFade(1, FadeInDuration));
        sequence.OnComplete(() => Main.Loop.OnUpdate += UpdateProgress);
    }

    public override void FadeOutLoading()
    {
        Main.Loop.OnUpdate -= UpdateProgress;
        sequence?.Kill();
        sequence = DOTween.Sequence();
        sequence.Append(_cg.DOFade(0, FadeOutDuration));
        sequence.OnComplete(() => gameObject.SetActive(false));
    }

    #endregion

    #region Loading Progress

    // 매 프레임 로딩 진행률 갱신
    private void UpdateProgress(float deltaTime)
    {
        _showTime += deltaTime;
        _imgLoadingBar.SetFill(Mathf.Clamp01(_showTime / LoadingTime));

        if (_showTime >= LoadingTime && Main.Loading.IsLoadingSDK)
        {
            Main.Loop.OnUpdate -= UpdateProgress;
            OnLoadingFinished();
        }
    }

    // 로딩 완료 처리
    private void OnLoadingFinished()
    {
        onLoadingComplete?.Invoke();

        Main.Scene.ChangeScene("LobbyScene");
        FadeOutLoading();
    }

    #endregion

    #region Lifecycle

    private void OnDisable()
    {
        Main.Loop.OnUpdate -= UpdateProgress;
    }

    #endregion
}
