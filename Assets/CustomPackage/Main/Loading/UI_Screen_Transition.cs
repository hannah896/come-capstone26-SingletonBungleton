using DG.Tweening;
using System;
using UnityEngine;

/// <summary>
/// 씬 전환 로딩 화면.
/// 게임씬 진입 시에는 WorldGen 진행률(로딩바 + 단계 텍스트)과 랜덤 팁을 표시하고,
/// 그 외 일반 전환에서는 단순 페이드로 동작한다.
/// (진행률/팁 UI 요소가 프리팹에 없으면 자동으로 페이드만 수행)
/// </summary>
public class UI_Screen_Transition : UI_Screen
{
    #region Constants

    private const float FadeOutDuration = 0.5f;
    private const float TipInterval = 3.5f; // 팁 교체 주기(초)

    #endregion

    #region Fields

    private CanvasGroup _cg;

    // 진행률/팁 UI (프리팹에 있으면 자동 연동, 없으면 null → 단순 페이드)
    [SerializeField] private UI_Image UI_Image_Progressbar;
    [SerializeField] private UI_Text UI_Text_ProgressText;
    [SerializeField] private UI_Text UI_Text_Tip;

    // 월드 생성 진행률 연동
    private WorldGenManager _boundWorldGen;
    private bool _subscribed;

    // 팁 로테이션
    private float _tipTimer;

    // 로딩 중 표시할 팁 (돈스타브류 생존)
    private static readonly string[] Tips =
    {
        "팁: 밤이 오기 전에 모닥불을 피워 어둠을 밝히세요.",
        "팁: 허기가 바닥나면 체력이 서서히 줄어듭니다.",
        "팁: 자원은 낮에 모으고, 밤에는 거점을 지키세요.",
        "팁: 도구는 내구도가 닳으면 부서집니다. 여분을 챙기세요.",
        "팁: 비에 젖으면 체온이 떨어질 수 있습니다.",
        "팁: 동료와 함께라면 생존 확률이 올라갑니다.",
    };

    #endregion

    #region Initialization

    public override bool Initialize()
    {
        if (!base.Initialize()) return false;

        _cg = gameObject.GetOrAddComponent<CanvasGroup>();

        // 선택적 UI 요소 — 프리팹에 있으면 진행률/팁을 표시, 없으면 페이드만\
        if (UI_Image_Progressbar == null)
            UI_Image_Progressbar = gameObject.FindChild<UI_Image>("Img_Bar_F");
        if (UI_Text_ProgressText == null)
            UI_Text_ProgressText = gameObject.FindChild<UI_Text>("Txt_Stage");
        if (UI_Text_Tip == null)
            UI_Text_Tip = gameObject.FindChild<UI_Text>("Txt_Tip");

        return true;
    }

    #endregion

    #region UI_Screen Overrides

    public override void Set(Action onLoadingComplete = null)
    {
        base.Set(onLoadingComplete);
        _cg.alpha = 1f;

        _subscribed = false;
        _boundWorldGen = null;
        _tipTimer = 0f;

        if (UI_Image_Progressbar != null) UI_Image_Progressbar.SetFill(0f);
        if (UI_Text_ProgressText != null) UI_Text_ProgressText.Text = string.Empty;
        ShowRandomTip();
    }

    public override void FadeInLoading()
    {
        base.FadeInLoading();
        Main.Loop.OnUpdate += OnLoadingUpdate;
    }

    public override void FadeOutLoading()
    {
        base.FadeOutLoading();
        Main.Loop.OnUpdate -= OnLoadingUpdate;
        Unsubscribe();
        sequence.Append(_cg.DOFade(0, FadeOutDuration));
    }

    #endregion

    #region Loading Update

    // 매 프레임: WorldGen 진행률 구독 + 팁 로테이션
    private void OnLoadingUpdate(float deltaTime)
    {
        // 게임씬 로딩 시 WorldGenManager가 동적 생성되면 진행률 구독 (1회)
        if (!_subscribed && WorldGenManager.Instance != null)
        {
            _boundWorldGen = WorldGenManager.Instance;
            _boundWorldGen.OnProgress += OnWorldGenProgress;
            _subscribed = true;
        }

        // 팁 주기적 교체
        _tipTimer += deltaTime;
        if (_tipTimer >= TipInterval)
        {
            _tipTimer = 0f;
            ShowRandomTip();
        }
    }

    private void OnWorldGenProgress(float value, string label)
    {
        if (UI_Image_Progressbar != null) UI_Image_Progressbar.SetFill(value);
        if (UI_Text_ProgressText != null) UI_Text_ProgressText.Text = label;
    }

    private void ShowRandomTip()
    {
        if (UI_Text_Tip == null || Tips.Length == 0) return;
        UI_Text_Tip.Text = Tips[UnityEngine.Random.Range(0, Tips.Length)];
    }

    private void Unsubscribe()
    {
        if (_boundWorldGen != null)
        {
            _boundWorldGen.OnProgress -= OnWorldGenProgress;
            _boundWorldGen = null;
        }
        _subscribed = false;
    }

    #endregion

    #region Lifecycle

    private void OnDestroy()
    {
        // 구독 해제는 OnDestroy에서 (안전망)
        Main.Loop.OnUpdate -= OnLoadingUpdate;
        Unsubscribe();
    }

    #endregion
}
