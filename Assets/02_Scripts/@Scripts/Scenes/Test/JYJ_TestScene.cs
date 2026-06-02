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

        // 플레이어 생성 — FP_CinemachineCamera는 Player.Start() 내부에서 자동 생성됨
        // Main Camera(CinemachineBrain 포함)는 ScreenManager가 MainCameraObject로 생성
        await Extensions.Instantiate<Player>("Player");
        //var go = new GameObject("ItemTest", typeof(ItemTest));
    }

    public override void ExitScene()
    {

    }
}