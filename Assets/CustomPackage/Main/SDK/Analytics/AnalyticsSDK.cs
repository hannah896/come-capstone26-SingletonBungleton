using Cysharp.Threading.Tasks;
using UnityEngine;

public abstract class AnalyticsSDK
{
    public abstract AnalyticsType Type { get; }
    public abstract bool IsInitializedSDK();
    public abstract UniTask InitializeSDK();
    public abstract void LogEvent(EventData data);
}
