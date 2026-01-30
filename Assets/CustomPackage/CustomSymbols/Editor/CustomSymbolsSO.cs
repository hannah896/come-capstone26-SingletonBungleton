using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SymbolsSettings", menuName = "CustomEditor/CustomSymbols")]
public class CustomSymbolsSO : ScriptableObject
{
    [Header("Custom All Symbols")] // 등록된 모든 심볼을 저장
    public List<string> customAllSymbols = new();
    
    [Header("All Platform Symbols")] // 플랫폼 상관 없이 적용할 심볼
    public List<string> allPlatformSymbols = new();
    
    [Header("Window Platform Symbols")] // 윈도우 플랫폼에 적용할 심볼
    public List<string> windowPlatformSymbols = new();
    
    [Header("Mac Platform Symbols")] // 맥 플랫폼에 적용할 심볼
    public List<string> macPlatformSymbols = new();
    
    [Header("AOS Platform Symbols")] // AOS 플랫폼에 적용할 심볼
    public List<string> aosPlatformSymbols = new();
    
    [Header("IOS Platform Symbols")] // IOS 플랫폼에 적용할 심볼
    public List<string> iosPlatformSymbols = new();
}
