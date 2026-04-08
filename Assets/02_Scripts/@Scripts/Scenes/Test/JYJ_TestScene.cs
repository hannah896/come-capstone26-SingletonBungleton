using Cysharp.Threading.Tasks;
using JSAM;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class JYJ_TestScene : SceneBase
{

    public override async UniTask EnterScene(CancellationToken token)
    {
        await Extensions.Instantiate<AudioManager>("AudioManager");
        //var go = new GameObject("ItemTest", typeof(ItemTest));
    }

    public override void ExitScene()
    {

    }
}