#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;

public static class Editor_SymbolDefiner
{
    [MenuItem("Tools/Setting/Setup Project Symbols")]
    public static void Setup()
    {
        string[] symbolsToAdd = {
            "UNITASK_DOTWEEN_SUPPORT",
        };

        var targets = new[] {
            NamedBuildTarget.Standalone,
            NamedBuildTarget.Android,
            NamedBuildTarget.iOS,
            NamedBuildTarget.Server
        };

        foreach (var target in targets)
        {
            string currentSymbols = PlayerSettings.GetScriptingDefineSymbols(target);
            foreach (var symbol in symbolsToAdd)
            {
                if (!currentSymbols.Contains(symbol))
                {
                    currentSymbols = string.IsNullOrEmpty(currentSymbols)
                        ? symbol
                        : $"{currentSymbols};{symbol}";
                }
            }
            PlayerSettings.SetScriptingDefineSymbols(target, currentSymbols);
        }
        foreach(var target in symbolsToAdd)
        {
            UnityEngine.Debug.Log($"모든 플랫폼에 {target} 심볼이 추가되었습니다.");
        }
    }
}
#endif