using Cysharp.Threading.Tasks;
using UnityEngine;

public interface ILoadingSDK
{
    public bool IsInitializedSDK();
    public UniTask InitializeSDK();
}
