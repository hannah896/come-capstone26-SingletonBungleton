using System;
using Cysharp.Threading.Tasks;
using Firebase;
using Firebase.Analytics;
using UnityEngine;

public class AnalyticsSDK_Firebase : AnalyticsSDK
{
    #region Initialized
    
    private bool _isInitializedSDK = false;
    public override bool IsInitializedSDK() => _isInitializedSDK;
    public override async UniTask InitializeSDK()
    {
        try
        {
            var dependencyStatus = await FirebaseApp.CheckAndFixDependenciesAsync();
            if (dependencyStatus == DependencyStatus.Available)
            {
#if UNITY_IOS && !UNITY_EDITOR
                    await UniTask.Delay(1000); // 최소 1초 이상
#endif
                FirebaseApp app = FirebaseApp.DefaultInstance;
                _isInitializedSDK = true;
                Debug.Log("[Firebase] Initialized");
            }
            else
            {
                Debug.LogError("[Firebase] Fail Initialized: " + dependencyStatus);
            }
            await UniTask.WaitForSeconds(1f);
        }
        catch (Exception e)
        {
            Debug.LogError("[Firebase] Fail Initialized: " + e.Message);
            _isInitializedSDK = true;
        } 
    }
    
  #endregion

    #region Analytics

    public override AnalyticsType Type => AnalyticsType.Firebase;
    public override void LogEvent(EventData data)
    {
        string eventName = data.EventName.ToLower();

        if (!string.IsNullOrEmpty(data.ParamValue))
        {
            Parameter parameter = new(eventName, data.ParamValue);
            FirebaseAnalytics.LogEvent(eventName, parameter);
        }
        else
        {
            FirebaseAnalytics.LogEvent(eventName);
        }
    }
    
  #endregion
}
