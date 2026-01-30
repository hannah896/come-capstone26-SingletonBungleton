using System;
using UnityEngine;

public static class TimeUtilities {

    #region Const
    
    public static long TimeMin => DateTime.MinValue.ToBinary();
    public static long TimeCurrent => DateTime.UtcNow.ToBinary();

    #endregion

    
    
    // 대상 시각까지 남은 시간(초)을 구함.
    public static int GetRemaingSeconds(this long targetTime) {
        if (targetTime == TimeMin) return 0;
        try {
            DateTime target = DateTime.FromBinary(targetTime);
            DateTime current = DateTime.UtcNow;
            if (target <= current) return 0;
            return (int)(target - current).TotalSeconds;
        }
        catch (Exception) {
            return 0;
        }
    }
    
    // 주어진 시각으로부터 대상 시간(분)만큼 지난 시각을 구함.
    public static long GetLaterTime(this long time, int min) {
        if (time == TimeMin) return TimeCurrent;
        try {
            return DateTime.FromBinary(time).AddMinutes(min).ToBinary();
        }
        catch (Exception) {
            return Def.TimeCurrent;
        }
    }
    
    // 현재 시각으로부터 대상 시간(분)만큼 지난 시각을 구함.
    public static long GetLaterTime(this int min) {
        if (min <= 0) return TimeMin;
        try {
            return DateTime.UtcNow.AddMinutes(min).ToBinary();
        }
        catch (Exception) {
            return TimeMin;
        }
    }
    
    // 주어진 시각으로부터 현재 얼마만큼의 시간(분)이 지났는지 구함.
    public static float GetElapsedTime(this long time) {
        if (time == TimeMin) return 0;
        try {
            return (float)(DateTime.UtcNow - DateTime.FromBinary(time)).TotalMinutes;
        }
        catch (Exception) {
            return 0;
        }
    }
    
}

public struct NanaTime {
    public int Min;
    public int Sec;

    public NanaTime(int min, int sec) {
        Min = min;
        Sec = sec;
    }

    public NanaTime(int sec) {
        Min = sec / 60;
        Sec = sec % 60;
    }

    public bool IsNana => Min == 0 && Sec == 0;
    
    public string FormatTime(string prefix = "") {
        return $"{prefix}{Min:D2}:{Sec:D2}";
    }
}