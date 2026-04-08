using Cysharp.Threading.Tasks;
using JSAM;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;

public class PHN_TestScene : SceneBase
{
    public override async UniTask EnterScene(CancellationToken token)
    {
        await Extensions.Instantiate<AudioManager>("AudioManager");
        await Extensions.Instantiate<Player>("Player");
    }

    public override void ExitScene()
    {
    }
}
