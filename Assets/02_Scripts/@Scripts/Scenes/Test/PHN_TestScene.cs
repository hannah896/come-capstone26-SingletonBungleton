using Cysharp.Threading.Tasks;
using JSAM;
using System.Threading;
using UnityEngine;

public class PHN_TestScene : SceneBase
{
    public override async UniTask EnterScene(CancellationToken token)
    {
        // 테스트 씬에서도 게임 로직(OnGameUpdate)이 돌도록 Testing 상태로 진입한다.
        GameScene.GameProcessing = GameProcessing.Testing;

        await Extensions.Instantiate<AudioManager>("AudioManager");

        // 플레이어 생성 — FP_CinemachineCamera는 Player.Start() 내부에서 자동 생성됨
        // Main Camera(CinemachineBrain 포함)는 ScreenManager가 MainCameraObject로 생성
        await Extensions.Instantiate<Player>("Player");
    }

    public override void ExitScene()
    {
        // 테스트 상태가 다른 씬으로 새지 않도록 초기화한다.
        GameScene.GameProcessing = GameProcessing.None;
    }
}
