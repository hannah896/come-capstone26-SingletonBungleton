using Cysharp.Threading.Tasks;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Reflection;

public class UI_Openner : MonoBehaviour
{
    [SerializeField] public string _scriptName;
    [SerializeField] public string _uiKey;

    private static readonly Dictionary<string, MethodInfo> _methodCache = new();

    public void OnClick_Popup() => OnClick_PopupAsync().Forget();
    public void OnClick_View() => OnClick_ViewAsync().Forget();

    public async UniTask OnClick_PopupAsync()
    {
        await OpenInternal("ShowPopup", new object[] { _uiKey, true, -1f, true });
    }

    public async UniTask OnClick_ViewAsync()
    {
        await OpenInternal("ShowView", new object[] { _uiKey });
    }

    private async UniTask OpenInternal(string methodName, object[] parameters)
    {
        string cacheKey = $"{_scriptName}_{methodName}";

        if (!_methodCache.TryGetValue(cacheKey, out MethodInfo genericMethod))
        {
            Type uiType = Type.GetType(_scriptName);
            if (uiType == null)
            {
                Debug.LogError($"[OpenUI] 타입을 찾을 수 없습니다: {_scriptName}");
                return;
            }

            List<Type> paramTypes = new List<Type>();
            foreach (var p in parameters) paramTypes.Add(p.GetType());
            paramTypes.Add(typeof(System.Threading.CancellationToken));

            MethodInfo method = typeof(Extensions).GetMethod(methodName,
                BindingFlags.Static | BindingFlags.Public,
                null,
                paramTypes.ToArray(),
                null);

            if (method == null)
            {
                Debug.LogError($"[OpenUI] Extensions.{methodName}를 찾을 수 없습니다.");
                return;
            }

            genericMethod = method.MakeGenericMethod(uiType);
            _methodCache[cacheKey] = genericMethod;
        }

        object[] finalParameters = new object[parameters.Length + 1];
        Array.Copy(parameters, finalParameters, parameters.Length);
        finalParameters[parameters.Length] = this.GetCancellationTokenOnDestroy();

        var taskObject = genericMethod.Invoke(null, finalParameters);

        if (taskObject is UniTask task)
        {
            await task;
        }
    }
}