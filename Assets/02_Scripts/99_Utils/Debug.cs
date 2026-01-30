using System;
using System.Diagnostics;
using UnityEngine;
using Object = UnityEngine.Object;

public class Debug
{
    private const string DEBUG_SYMBOL = "UNITY_EDITOR";

    [Conditional(DEBUG_SYMBOL)]
    [HideInCallstack]
    public static void Log(string message)
    {
        UnityEngine.Debug.Log(message);
    }

    [Conditional(DEBUG_SYMBOL)]
    [HideInCallstack]
    public static void Log(string message, Object context)
    {
        UnityEngine.Debug.Log(message, context);
    }

    [Conditional(DEBUG_SYMBOL)]
    [HideInCallstack]
    public static void LogWarning(string message)
    {
        UnityEngine.Debug.Log(message);
    }

    [Conditional(DEBUG_SYMBOL)]
    [HideInCallstack]
    public static void LogWarning(string message, Object context)
    {
        UnityEngine.Debug.Log(message, context);
    }

    [Conditional(DEBUG_SYMBOL)]
    [HideInCallstack]
    public static void LogError(string message)
    {
        UnityEngine.Debug.Log(message);
    }

    [Conditional(DEBUG_SYMBOL)]
    [HideInCallstack]
    public static void LogError(string message, Object context)
    {
        UnityEngine.Debug.Log(message, context);
    }

    [Conditional(DEBUG_SYMBOL)]
    [HideInCallstack]
    public static void LogException(Exception message)
    {
        UnityEngine.Debug.LogException(message);
    }
}
