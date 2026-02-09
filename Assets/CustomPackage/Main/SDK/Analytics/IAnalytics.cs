using System;
using UnityEngine;

public interface IAnalytics
{
    public AnalyticsType Type { get; }
    public void LogEvent(EventData data);
}

[Flags]
public enum AnalyticsType
{
    None = 0,
    Firebase = 1 << 0,
}