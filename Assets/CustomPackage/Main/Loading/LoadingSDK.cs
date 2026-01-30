using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

public abstract class LoadingSDK : CoreManager, ILoadingSDK
{
    public abstract bool IsInitializedSDK();

    public abstract UniTask InitializeSDK();

    protected override async UniTask OnInitializeAsync()
    {
        await base.OnInitializeAsync();
        Main.Loading.LoadingSDKs.Add(this);
    }
}
