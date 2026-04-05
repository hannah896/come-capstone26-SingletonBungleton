using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

// Scene 이름과 동일한 클래스명이어야 합니다.
public class KGB_TestScene : SceneBase
{
    public override async UniTask EnterScene(CancellationToken token)
    {
        // 씬 진입 시 실행될 로직 (필요 시 작성)
        Debug.Log("[KGB_TestScene] Scene Entered");

        

    }

    public override void ExitScene()
    {
        // 씬 나갈 때 실행될 로직
        Debug.Log("[KGB_TestScene] Scene Exited");

    }
}