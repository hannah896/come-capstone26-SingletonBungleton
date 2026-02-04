using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// 외부 AnalyticsSDK와 연결해 이벤트를 전달하는 매니저.
/// Firebase, GameAnalytics, Singular 등의 분석 SDK를 통합 관리합니다.
/// </summary>
/// <remarks>
/// PurchaseEventArgs에 IAP버전 Warning 알림이 있다면 IAP를 4.16버전으로 맞출 것.
/// </remarks>
public class LoadingAnalyticsSDK : LoadingSDK
{
    #region Fields

    // Firebase 분석 SDK
    private AnalyticsSDK_Firebase _firebase = new();

    // 등록된 분석 SDK 목록
    private List<AnalyticsSDK> _analytics = new();

    #endregion

    #region Initialization

    protected override async UniTask OnInitializeAsync()
    {
        await base.OnInitializeAsync();

        // Field에 등록해둔 AnalyticsSDK들을 읽고 초기화 및 캐싱
        foreach (FieldInfo fieldInfo in typeof(LoadingAnalyticsSDK).GetFields(BindingFlags.NonPublic |
                                                                              BindingFlags.Instance))
        {
            AnalyticsSDK analytics = fieldInfo.GetValue(Main.AnalyticsSDK) as AnalyticsSDK;
            if (analytics == null) continue;
            _analytics.Add(analytics);
        }
    }

    /// <summary>
    /// 모든 SDK가 초기화되었는지 확인합니다.
    /// </summary>
    public override bool IsInitializedSDK()
    {
        foreach (AnalyticsSDK analytics in _analytics)
        {
            if (!analytics.IsInitializedSDK()) return false;
        }

        return true;
    }

    /// <summary>
    /// 모든 등록된 SDK를 초기화합니다.
    /// </summary>
    public override UniTask InitializeSDK()
    {
        foreach (AnalyticsSDK analytics in _analytics)
        {
            _ = analytics.InitializeSDK();
        }

        return UniTask.CompletedTask;
    }

    #endregion

    #region Event Logging

    /// <summary>
    /// 일반 로그 이벤트를 전송합니다.
    /// </summary>
    /// <param name="eventName">전송할 이벤트 제목(필수)</param>
    /// <param name="paramValue">전송할 이벤트 값(선택 사항)</param>
    /// <param name="analyticsType">전송할 Analytics 선택</param>
    public void LogEvent(string eventName, string paramValue = null, AnalyticsType analyticsType = AnalyticsType.Firebase)
    {
        EventData data = CreateEventData(eventName, paramValue);
        LogEvent(data, analyticsType);
        Debug.Log($"Try LogEvent Name: {eventName}");
    }

    #endregion

    #region Internal Methods

    // 일반 이벤트 데이터 전송
    private void LogEvent(EventData data, AnalyticsType type)
    {
        if (type == AnalyticsType.None) return;
        foreach (AnalyticsSDK analytics in _analytics)
        {
            if (!CheckEnableSDK(type, analytics)) continue;

            try
            {
                analytics.LogEvent(data);
            }
            catch (Exception e)
            {
                Debug.LogError($"Analytics SDK Failed LogEvent: {analytics.Type}, Exception: {e.Message}");
            }
        }
    }

    // 해당 SDK가 등록됐는지, 활성화 상태인지 확인
    private bool CheckEnableSDK(AnalyticsType type, AnalyticsSDK sdk)
    {
        // type에 포함되지 않으면 제외
        if ((sdk.Type & type) == 0) return false;

        // sdk가 초기화되지 않았다면 제외
        if (!sdk.IsInitializedSDK())
        {
            Debug.LogError($"Analytics SDK not initialized: {sdk.Type}");
            return false;
        }

        return true;
    }

    #endregion

    #region Data Creation

    // 일반 이벤트 데이터 생성
    private EventData CreateEventData(string eventName, string paramValue = null)
    {
        return new EventData.Builder(eventName)
            .SetParamValue(paramValue)
            .Build();
    }

    #endregion
}
